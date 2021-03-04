using System;
using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;

namespace RailgunNet.Connection.Client
{
    public class RailClientRoom : RailRoom
    {
        /// <summary>
        ///     The local Railgun client.
        /// </summary>
        private readonly RailClient client;

        private readonly IRailEventConstruction eventCreator;

        /// <summary>
        ///     All known entities, either in-world or pending.
        /// </summary>
        private readonly Dictionary<EntityId, RailEntityClient> knownEntities;

        /// <summary>
        ///     The local controller for predicting control and authority.
        ///     This is a dummy peer that can't send or receive events.
        /// </summary>
        private readonly RailController localPeer;

        /// <summary>
        ///     Entities that are waiting to be added to the world.
        /// </summary>
        private readonly Dictionary<EntityId, RailEntityClient> pendingEntities;

        private List<RailEntityClient> toRemoveBuffer; // Pre-allocated removal list
        private List<RailEntityClient> toUpdateBuffer; // Pre-allocated update list

        public RailClientRoom(RailResource resource, RailClient client) : base(resource, client)
        {
            eventCreator = resource;
            toUpdateBuffer = new List<RailEntityClient>();
            toRemoveBuffer = new List<RailEntityClient>();

            IEqualityComparer<EntityId> entityIdComparer = EntityId.CreateEqualityComparer();

            pendingEntities = new Dictionary<EntityId, RailEntityClient>(entityIdComparer);
            knownEntities = new Dictionary<EntityId, RailEntityClient>(entityIdComparer);
            localPeer = new RailController(null);
            this.client = client;
        }

        /// <summary>
        ///     Returns all locally-controlled entities in the room.
        /// </summary>
        public IEnumerable<RailEntityBase> LocalEntities => localPeer.ControlledEntities;

        /// <summary>
        ///     Queues an event to broadcast to the server with a number of retries.
        /// </summary>
        public void RaiseEvent<T>(Action<T> initializer, ushort attempts = 3)
            where T : RailEvent
        {
            RailDebug.Assert(client.ServerPeer != null);
            if (client.ServerPeer != null)
            {
                T evnt = eventCreator.CreateEvent<T>();
                initializer(evnt);
                client.ServerPeer.SendEvent(evnt, attempts, false);
            }
        }

        /// <summary>
        ///     Updates the room a number of ticks. If we have entities waiting to be
        ///     added, this function will check them and add them if applicable.
        /// </summary>
        public void ClientUpdate(Tick localTick, Tick estimatedServerTick)
        {
            Tick = estimatedServerTick;
            UpdatePendingEntities(estimatedServerTick);
            OnPreRoomUpdate(estimatedServerTick);

            // Collect the entities in the priority order and
            // separate them out for either update or removal
            foreach (RailEntityBase railEntityBase in Entities.Values)
            {
                RailEntityClient entity = (RailEntityClient)railEntityBase;
                if (entity.ShouldRemove)
                {
                    toRemoveBuffer.Add(entity);
                }
                else
                {
                    toUpdateBuffer.Add(entity);
                }
            }

            // Wave 0: Remove all sunsetted entities
            foreach (var railEntityClient in toRemoveBuffer)
            {
                if (RemoveEntity(railEntityClient))
                    knownEntities.Remove(railEntityClient.Id);
            }

            toRemoveBuffer.Clear();

            // Wave 1: Start/initialize all entities
            int updateCount = toUpdateBuffer.Count;
            for (var index = 0; index < updateCount; index++)
                toUpdateBuffer[index].PreUpdate();

            // Wave 2: Update all entities
            for (var index = 0; index < updateCount; index++)
                toUpdateBuffer[index].ClientUpdate(localTick);

            // Wave 3: Post-update all entities
            for (var index = 0; index < updateCount; index++)
                toUpdateBuffer[index].PostUpdate();

            toUpdateBuffer.Clear();
            OnPostRoomUpdate(estimatedServerTick);
        }

        /// <summary>
        ///     Returns true iff we stored the delta.
        /// </summary>
        public bool ProcessDelta(RailStateDelta delta)
        {
            if (knownEntities.TryGetValue(delta.EntityId, out RailEntityClient entity) == false)
            {
                RailDebug.Assert(delta.IsFrozen == false, "Frozen unknown entity");
                if (delta.IsFrozen || delta.IsRemoving) return false;

                entity = delta.ProduceEntity(Resource) as RailEntityClient;
                if (entity == null)
                {
                    throw new TypeAccessException(
                        "Got unexpected instance from RailResource. Internal error in type RailRegistry and/or RailResource.");
                }

                var serverSendRate = client.ServerPeer.RemoteSendRate;
                entity.Initialize(delta.EntityId, serverSendRate);

                entity.PrimeState(delta);
                pendingEntities.Add(entity.Id, entity);
                knownEntities.Add(entity.Id, entity);
            }

            // If we're already removing the entity, we don't care about other deltas
            bool stored = false;
            if (entity.IsRemoving == false) stored = entity.ReceiveDelta(delta);
            return stored;
        }

        /// <summary>
        ///     Checks to see if any pending entities can be added to the world and
        ///     adds them if applicable.
        /// </summary>
        private void UpdatePendingEntities(Tick serverTick)
        {
            foreach (RailEntityClient entity in pendingEntities.Values)
            {
                if (!entity.HasReadyState(serverTick)) continue;

                // Note: We're using ToRemove here to remove from the *pending* list
                toRemoveBuffer.Add(entity);

                // If the entity was removed while pending, forget about it
                Tick removeTick = entity.RemovedTick; // Can't use ShouldRemove
                if (removeTick.IsValid && removeTick <= serverTick)
                {
                    knownEntities.Remove(entity.Id);
                }
                else
                {
                    RegisterEntity(entity);
                }
            }

            foreach (RailEntityClient entity in toRemoveBuffer)
            {
                pendingEntities.Remove(entity.Id);
            }

            toRemoveBuffer.Clear();
        }

        public void RequestControlUpdate(RailEntityClient entity, RailStateDelta delta)
        {
            // Can't infer anything if the delta is an empty frozen update
            if (delta.IsFrozen) return;

            if (delta.IsRemoving)
            {
                if (entity.Controller != null) localPeer.RevokeControlInternal(entity);
            }
            else if (delta.HasControllerData)
            {
                if (entity.Controller == null) localPeer.GrantControlInternal(entity);
            }
            else
            {
                if (entity.Controller != null) localPeer.RevokeControlInternal(entity);
            }
        }
    }
}
