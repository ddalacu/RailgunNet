/*
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

using System;
using System.Collections.Generic;
using RailgunNet.Connection;
using RailgunNet.Factory;
using RailgunNet.System.Types;

namespace RailgunNet.Logic
{
    public abstract class RailRoom
    {
        private readonly RailConnection connection;
        private readonly Dictionary<EntityId, IRailEntity> entities;

        public readonly RailResource resource;
        protected List<RailEntity> toRemove; // Pre-allocated removal list

        protected List<RailEntity> toUpdate; // Pre-allocated update list

        protected RailRoom(RailResource resource, RailConnection connection)
        {
            this.resource = resource;
            this.connection = connection;
            entities =
                new Dictionary<EntityId, IRailEntity>(
                    EntityId.CreateEqualityComparer());
            Tick = Tick.INVALID;

            toUpdate = new List<RailEntity>();
            toRemove = new List<RailEntity>();
        }

        public object UserData { get; set; }

        /// <summary>
        ///     The current synchronized tick. On clients this will be the predicted
        ///     server tick. On the server this will be the authoritative tick.
        /// </summary>
        public Tick Tick { get; protected set; }

        /// <summary>
        ///     All of the entities currently added to this room.
        /// </summary>
        public Dictionary<EntityId, IRailEntity>.ValueCollection Entities => entities.Values;

        /// <summary>
        ///     Fired before all entities have updated, for updating global logic.
        /// </summary>
        public event Action<Tick> PreRoomUpdate;

        /// <summary>
        ///     Fired after all entities have updated, for updating global logic.
        /// </summary>
        public event Action<Tick> PostRoomUpdate;

        /// <summary>
        ///     Notifies that we removed an entity.
        /// </summary>
        public event Action<IRailEntity> EntityRemoved;

        protected virtual void HandleRemovedEntity(EntityId entityId)
        {
        }

        public bool TryGet(EntityId id, out IRailEntity value)
        {
            return entities.TryGetValue(id, out value);
        }

        public TEvent CreateEvent<TEvent>()
            where TEvent : RailEvent, new()
        {
            return RailEvent.Create<TEvent>(resource);
        }

        public void Initialize(Tick tick)
        {
            Tick = tick;
        }

        protected void OnPreRoomUpdate(Tick tick)
        {
            PreRoomUpdate?.Invoke(tick);
        }

        protected void OnPostRoomUpdate(Tick tick)
        {
            PostRoomUpdate?.Invoke(tick);
        }

        protected IEnumerable<RailEntity> GetAllEntities()
        {
            // TODO: This makes multiple full passes, could probably optimize
            foreach (RailConfig.RailUpdateOrder order in RailConfig.Orders)
            foreach (RailEntity entity in entities.Values)
                if (entity.UpdateOrder == order)
                    yield return entity;
        }

        protected void RegisterEntity(RailEntity entity)
        {
            entities.Add(entity.Id, entity);
            entity.Room = this;
        }

        protected void RemoveEntity(RailEntity entity)
        {
            if (entities.ContainsKey(entity.Id))
            {
                entities.Remove(entity.Id);
                entity.Shutdown();
                entity.Room = null;
                // TODO: Pooling entities?

                HandleRemovedEntity(entity.Id);
                EntityRemoved?.Invoke(entity);
            }
        }
    }
}