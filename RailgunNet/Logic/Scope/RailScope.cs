﻿/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */

using System.Collections.Generic;
using System.Linq;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Types;
using RailgunNet.Util;

namespace RailgunNet.Logic.Scope
{
    [OnlyIn(Component.Server)]
    public class RailScope
    {
        private readonly RailView ackedByClient = new RailView();
        private readonly List<RailStateDelta> activeList = new List<RailStateDelta>();

        // Pre-allocated reusable fill lists
        private readonly List<KeyValuePair<float, IRailEntity>> entryList = new List<KeyValuePair<float, IRailEntity>>();
        private readonly List<RailStateDelta> frozenList = new List<RailStateDelta>();
        private readonly RailView lastSent = new RailView();

        private readonly RailController owner;
        private readonly EntityPriorityComparer priorityComparer = new EntityPriorityComparer();
        private readonly List<RailStateDelta> removedList = new List<RailStateDelta>();
        private readonly IRailStateConstruction stateCreator;

        public RailScope(RailController owner, IRailStateConstruction stateCreator)
        {
            Evaluator = new RailScopeEvaluator();
            this.owner = owner;
            this.stateCreator = stateCreator;
        }

        public RailScopeEvaluator Evaluator { get; set; }

        public bool Includes(
            RailEvent evnt)
        {
            return Evaluator.Evaluate(evnt);
        }

        public void PopulateDeltas(
            Tick serverTick,
            RailPacketToClient packetToClient,
            IEnumerable<IRailEntity> activeEntities,
            IEnumerable<IRailEntity> removedEntities)
        {
            ProduceScoped(serverTick, activeEntities);
            ProduceRemoved(owner, removedEntities);

            packetToClient.Populate(activeList, frozenList, removedList);

            removedList.Clear();
            frozenList.Clear();
            activeList.Clear();
        }

        public void IntegrateAcked(RailView packetView)
        {
            ackedByClient.Integrate(packetView);
        }

        public void RegisterSent(EntityId entityId, Tick tick, bool isFrozen)
        {
            // We don't care about the local tick on the server side
            lastSent.RecordUpdate(entityId, tick, Tick.INVALID, isFrozen);
        }

        private bool GetPriority(
            IRailEntity entity,
            Tick current,
            out float priority)
        {
            RailViewEntry lastSent = this.lastSent.GetLatest(entity.Id);
            RailViewEntry lastAcked = ackedByClient.GetLatest(entity.Id);

            int ticksSinceSend = int.MaxValue;
            int ticksSinceAck = int.MaxValue;

            if (lastSent.IsValid)
                ticksSinceSend = current - lastSent.LastReceivedTick;
            if (lastAcked.IsValid)
                ticksSinceAck = current - lastAcked.LastReceivedTick;

            return EvaluateEntity(
                entity,
                ticksSinceSend,
                ticksSinceAck,
                out priority);
        }

        /// <summary>
        ///     Divides the active entities into those that are in scope and those
        ///     out of scope. If an entity is out of scope and hasn't been acked as
        ///     such by the client, we will add it to the outgoing frozen delta list.
        ///     Otherwise, if an entity is in scope we will add it to the sorted
        ///     active delta list.
        /// </summary>
        private void ProduceScoped(
            Tick serverTick,
            IEnumerable<IRailEntity> activeEntities)
        {
            entryList.Clear();

            foreach (IRailEntity entity in activeEntities)
            {
                if (entity.IsRemoving)
                {
                }
                // Controlled entities are always in scope to their controller
                else if (entity.Controller == owner)
                {
                    entryList.Add(
                        new KeyValuePair<float, IRailEntity>(float.MinValue, entity));
                }
                else if (GetPriority(entity, serverTick, out float priority))
                {
                    entryList.Add(
                        new KeyValuePair<float, IRailEntity>(priority, entity));
                }
                else if (entity.CanFreeze)
                {
                    // We only want to send a freeze state if we aren't already frozen
                    RailViewEntry latest = ackedByClient.GetLatest(entity.Id);
                    if (latest.IsFrozen == false)
                        frozenList.Add(
                            RailStateDelta.CreateFrozen(
                                stateCreator,
                                serverTick,
                                entity.Id));
                }
            }

            entryList.Sort(priorityComparer);
            foreach (KeyValuePair<float, IRailEntity> entry in entryList)
            {
                RailViewEntry latest = ackedByClient.GetLatest(entry.Value.Id);

                // Force a complete update if the entity is frozen so it unfreezes
                // TODO: Currently if we're unfreezing we force the server to send a
                //       delta with the FULL mutable dataset. There is probably a
                //       less wasteful option, like having clients send back
                //       what tick they last received a non-frozen packetToClient on.
                //       However, this would cause some tedious tick comparison.
                //       Should investigate a smarter way to handle this later.
                RailStateDelta delta =
                    entry.Value.AsBase.ProduceDelta(
                        stateCreator,
                        latest.LastReceivedTick,
                        owner,
                        latest.IsFrozen);

                if (delta != null)
                    activeList.Add(delta);
            }
        }

        /// <summary>
        ///     Produces deltas for all non-acked removed entities.
        /// </summary>
        private void ProduceRemoved(
            RailController target,
            IEnumerable<IRailEntity> removedEntities)
        {
            foreach (IRailEntity entity in removedEntities)
            {
                RailViewEntry latest = ackedByClient.GetLatest(entity.Id);

                // Note: Because the removed tick is valid, this should force-create
                if (latest.IsValid && latest.LastReceivedTick < entity.AsBase.RemovedTick)
                    removedList.Add(
                        entity.AsBase.ProduceDelta(
                            stateCreator,
                            latest.LastReceivedTick,
                            target,
                            false));
            }
        }

        private bool EvaluateEntity(
            IRailEntity entity,
            int ticksSinceSend,
            int ticksSinceAck,
            out float priority)
        {
            return
                Evaluator.Evaluate(
                    entity,
                    ticksSinceSend,
                    ticksSinceAck,
                    out priority);
        }

        private class EntityPriorityComparer :
            Comparer<KeyValuePair<float, IRailEntity>>
        {
            private readonly Comparer<float> floatComparer;

            public EntityPriorityComparer()
            {
                floatComparer = Comparer<float>.Default;
            }

            public override int Compare(
                KeyValuePair<float, IRailEntity> x,
                KeyValuePair<float, IRailEntity> y)
            {
                return floatComparer.Compare(x.Key, y.Key);
            }
        }

        public Tick GetLastSent(EntityId entityId)
        {
            return lastSent.GetLatest(entityId).LastReceivedTick;
        }

        public Tick GetLastAckedByClient(EntityId entityId)
        {
            if (entityId == EntityId.INVALID)
                return Tick.INVALID;
            return ackedByClient.GetLatest(entityId).LastReceivedTick;
        }

        public bool IsPresentOnClient(EntityId entityId)
        {
            return GetLastAckedByClient(entityId).IsValid;
        }
    }
}
