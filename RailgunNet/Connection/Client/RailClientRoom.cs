using System;
using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using UnityEngine;

namespace RailgunNet.Connection.Client
{
    public class RailClientRoom : RailRoom<IClientEntity>
    {
        /// <summary>
        ///     The local Railgun client.
        /// </summary>
        private readonly RailClient _client;

        /// <summary>
        ///     All known entities, either in-world or pending.
        /// </summary>
        private readonly Dictionary<EntityId, IClientEntity> _knownEntities;

        /// <summary>
        ///     Entities that are waiting to be added to the world.
        /// </summary>
        private readonly Dictionary<EntityId, IClientEntity> _pendingEntities;


        public HashSet<IClientEntity> _controlledEntities = new HashSet<IClientEntity>();

        public RailClient Client => _client;

        public RailResource Resource { get; }

        public ISet<IClientEntity> ControlledEntities => _controlledEntities;


        public RailClientRoom(RailResource resource, RailClient client) : base()
        {
            Resource = resource;

            _pendingEntities = new Dictionary<EntityId, IClientEntity>(EntityId.ComparerInstance);
            _knownEntities = new Dictionary<EntityId, IClientEntity>(EntityId.ComparerInstance);
            _client = client;
        }

        /// <summary>
        ///     Queues an event to broadcast to the server with a number of retries.
        /// </summary>
        public void RaiseEvent<T>(Action<T> initializer, ushort attempts = 3)
            where T : RailEvent
        {
            RaiseEvent<T>(initializer, EntityId.INVALID, attempts);
        }

        public void RaiseEvent<T>(Action<T> initializer, EntityId entityId, ushort attempts = 3)
            where T : RailEvent
        {
            RailDebug.Assert(_client.ServerPeer != null);
            if (_client.ServerPeer != null)
            {
                var evnt = Resource.CreateEvent<T>();
                initializer(evnt);
                evnt.TargetEntity = entityId;
                _client.ServerPeer.SendEvent(evnt, attempts);
            }
        }

        /// <summary>
        ///     Updates the room a number of ticks. If we have entities waiting to be
        ///     added, this function will check them and add them if applicable.
        /// </summary>
        public void ClientUpdate(Tick localTick)
        {
            UpdatePendingEntities(localTick);
            CallPreRoomUpdate(localTick);

            using var toRemove = TempRefList<IClientEntity>.Get();
            using var toUpdate = TempRefList<IClientEntity>.Get();

            // Collect the entities in the priority order and
            // separate them out for either update or removal
            foreach (var entity in Entities.Values)
            {
                if (entity.ShouldRemove(localTick))
                {
                    toRemove.Add(entity);
                }
                else
                {
                    toUpdate.Add(entity);
                }
            }

            // Wave 0: Remove all sunsetted entities
            var toRemoveCount = toRemove.Count;

            for (int i = 0; i < toRemoveCount; i++)
            {
                var railEntityClient = toRemove[i];
                if (RemoveEntity(railEntityClient))
                    _knownEntities.Remove(railEntityClient.Id);
            }

            // Wave 1: Start/initialize all entities
            int updateCount = toUpdate.Count;
            for (var index = 0; index < updateCount; index++)
            {
                if (toUpdate[index] is IPreTickUpdateListener preTickUpdateListener)
                    preTickUpdateListener.PreTickUpdate(localTick);
            }

            // Wave 2: Update all entities
            for (var index = 0; index < updateCount; index++)
                if (toUpdate[index] is ITickUpdateListener tickUpdateListener)
                    tickUpdateListener.TickUpdate(localTick);

            // Wave 3: Post-update all entities
            for (var index = 0; index < updateCount; index++)
                if (toUpdate[index] is IPostTickUpdateListener postTickUpdateListener)
                    postTickUpdateListener.PostTickUpdate(localTick);

            CallPostRoomUpdate(localTick);
        }

        /// <summary>
        ///     Returns true if we stored the delta.
        /// </summary>
        public bool ProcessDelta(RailStateDelta delta)
        {
            if (_knownEntities.TryGetValue(delta.EntityId, out var entity) == false)
            {
                RailDebug.Assert(delta.IsFrozen == false, "Frozen unknown entity");
                if (delta.IsFrozen || delta.IsRemoving) return false;

                var type = Resource.GetEntityType(delta.State);
                entity = (IClientEntity)Resource.CreateEntity(type);

                if (entity == null)
                {
                    throw new TypeAccessException(
                        "Got unexpected instance from RailResource. Internal error in type RailRegistry and/or RailResource.");
                }

                entity.Initialize(delta.EntityId, _client);

                entity.PrimeState(delta);
                _pendingEntities.Add(entity.Id, entity);
                _knownEntities.Add(entity.Id, entity);
            }

            // If we're already removing the entity, we don't care about other deltas
            bool stored = false;
            if (entity.IsRemoving() == false)
                stored = entity.ReceiveDelta(delta);
            return stored;
        }

        /// <summary>
        ///     Checks to see if any pending entities can be added to the world and
        ///     adds them if applicable.
        /// </summary>
        private void UpdatePendingEntities(Tick serverTick)
        {
            using var toRemove = TempRefList<IClientEntity>.Get();

            foreach (var entity in _pendingEntities.Values)
            {
                if (!entity.HasReadyState(serverTick))
                    continue;

                // Note: We're using ToRemove here to remove from the *pending* list
                toRemove.Add(entity);

                // If the entity was removed while pending, forget about it
                Tick removeTick = entity.RemovedTick; // Can't use ShouldRemove
                if (removeTick.IsValid && removeTick <= serverTick)
                {
                    _knownEntities.Remove(entity.Id);
                }
                else
                {
                    RegisterEntity(entity);
                }
            }

            var toRemoveCount = toRemove.Count;
            for (var i = 0; i < toRemoveCount; i++)
            {
                var entity = toRemove[i];
                _pendingEntities.Remove(entity.Id);
            }
        }


        protected void RegisterEntity(IClientEntity entity)
        {
            _entities.Add(entity.Id, entity);
            entity.Added(this);
            CallAddedEntity(entity);
        }

        protected bool RemoveEntity(IClientEntity entity)
        {
            if (_entities.ContainsKey(entity.Id))
            {
                _entities.Remove(entity.Id);
                entity.Removed();
                // TODO: Pooling entities?
                CallRemovedEntity(entity);
                return true;
            }

            return false;
        }

        public void HandleEvent(RailEvent @event)
        {
            if (@event.TargetEntity.IsValid)
            {
                if (Entities.TryGetValue(@event.TargetEntity, out var entity))
                {
                    entity.HandleEvent(@event);
                }
            }

            //todo send room event
        }
    }
}
