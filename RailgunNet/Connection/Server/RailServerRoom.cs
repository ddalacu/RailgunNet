using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using UnityEngine;

namespace RailgunNet.Connection.Server
{

    public class RailServerRoom : RailRoom<IServerEntity>
    {
        /// <summary>
        ///     All client controllers involved in this room.
        ///     Does not include the server's controller.
        /// </summary>
        private readonly List<RailServerPeer> _clients;

        /// <summary>
        ///     Used for creating new entities and assigning them unique ids.
        /// </summary>
        private EntityId _entityCounter = EntityId.START;

        /// <summary>
        ///     Entities that have been removed or are about to be.
        /// </summary>
        private readonly List<IServerEntity> removedEntities = new List<IServerEntity>();

        public IReadOnlyList<RailServerPeer> Clients => _clients;

        public IReadOnlyList<IServerEntity> RemovedEntities => removedEntities;

        public Tick Tick { get; private set; }

        public RailResource Resource { get; }

        public RailServerRoom(RailResource resource)
        {
            Resource = resource;
            _clients = new List<RailServerPeer>();
            Tick = Tick.START;
        }

        /// <summary>
        ///     Fired when a controller has been added (i.e. player join).
        ///     The controller has control of no entities at this point.
        /// </summary>
        public event Action<RailServerPeer> ClientJoined;

        /// <summary>
        ///     Fired when a controller has been removed (i.e. player leave).
        ///     This event fires before the controller has control of its entities
        ///     revoked (this is done immediately afterwards).
        /// </summary>
        public event Action<RailServerPeer> ClientLeft;

        /// <summary>
        ///     Adds an entity to the room. Cannot be done during the update pass.
        /// </summary>
        public T AddNewEntity<T>(Action<T> initializer = null)
            where T : IServerEntity
        {
            T entity = CreateEntity<T>();
            initializer?.Invoke(entity);
            RegisterEntity(entity);
            return entity;
        }

        /// <summary>
        ///     Marks an entity for removal from the room and presumably destruction.
        ///     This is deferred until the next frame.
        /// </summary>
        public void MarkForRemoval(EntityId id)
        {
            if (TryGet<IServerEntity>(id, out var entity))
            {
                MarkForRemoval(entity);
            }
        }

        /// <summary>
        ///     Marks an entity for removal from the room and presumably destruction.
        ///     This is deferred until the next frame.
        /// </summary>
        public void MarkForRemoval(IServerEntity entity)
        {
            if (entity.IsRemoving() == false)
            {
                entity.MarkForRemoval();
                removedEntities.Add(entity);
            }
        }

        /// <summary>
        ///     Queues an event to broadcast to all present clients.
        ///     Notice that due to the internal object pooling, the event will be cloned and managed
        ///     internally in each client peer. The <paramref name="evnt" /> will not be freed
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="attempts"></param>
        public void BroadcastEvent(
            [NotNull] RailEvent evnt,
            ushort attempts = 3)
        {
            foreach (var client in _clients)
            {
                var toSend = evnt.Clone(Resource);
                client.SendEvent(toSend, attempts);
            }
        }

        public void AddClient(RailServerPeer client)
        {
            var index = _clients.IndexOf(client);

            if (index == -1)
            {
                _clients.Add(client);
                ClientJoined?.Invoke(client);
            }
        }

        public bool RemoveClient(RailServerPeer client)
        {
            if (_clients.Remove(client))
            {
                ClientLeft?.Invoke(client);
                return true;
            }

            return false;
        }

        public void TickUpdate()
        {
            Tick = Tick.GetNext();
            CallPreRoomUpdate(Tick);

            using var toRemove = TempRefList<IServerEntity>.Get();
            using var toUpdate = TempRefList<IServerEntity>.Get();

            // Collect the entities in the priority order and
            // separate them out for either update or removal
            foreach (var entity in Entities.Values)
            {
                if (entity.ShouldRemove(Tick))
                {
                    toRemove.Add(entity);
                }
                else
                {
                    toUpdate.Add(entity);
                }
            }

            // Wave 0: Remove all sunsetted entities
            var removeCount = toRemove.Count;
            for (var i = 0; i < removeCount; i++)
                RemoveEntity(toRemove[i]);

            int updateBufferCount = toUpdate.Count;

            // Wave 1: Start/initialize all entities
            for (var index = 0; index < updateBufferCount; index++)
            {
                if (toUpdate[index] is IPreTickUpdateListener preTickUpdateListener)
                    preTickUpdateListener.PreTickUpdate(Tick);
            }

            // Wave 2: Update all entities
            for (var index = 0; index < updateBufferCount; index++)
            {
                if (toUpdate[index] is ITickUpdateListener tickUpdateListener)
                    tickUpdateListener.TickUpdate(Tick);
            }

            // Wave 3: Post-update all entities
            for (var index = 0; index < updateBufferCount; index++)
            {
                if (toUpdate[index] is IPostTickUpdateListener postTickUpdateListener)
                    postTickUpdateListener.PostTickUpdate(Tick);
            }

            CallPostRoomUpdate(Tick);
        }

        public void StoreStates()
        {
            foreach (var entity in Entities.Values)
            {
                entity.StoreRecord(Resource);
            }
        }

        private T CreateEntity<T>() where T : IServerEntity
        {
            var entity = Resource.CreateEntity<T>();
            entity.Initialize(_entityCounter);
            _entityCounter = _entityCounter.GetNext();
            return entity;
        }

        /// <summary>
        ///     Cleans out any removed entities from the removed list
        ///     if they have been acked by all clients.
        /// </summary>
        public void CleanRemovedEntities()
        {
            // TODO: Retire the Id in all of the views as well?
            for (var index = 0; index < removedEntities.Count; index++)
            {
                var entity = removedEntities[index];
                bool canRemove = true;

                var id = entity.Id;

                foreach (RailServerPeer peer in _clients)
                {
                    Tick lastSent = peer.Scope.GetLastSent(id);
                    if (lastSent.IsValid == false) continue; // Was never sent in the first place

                    Tick lastAcked = peer.Scope.GetLastAckedByClient(id);
                    if (lastAcked.IsValid && lastAcked >= entity.RemovedTick)
                    {
                        continue; // Remove tick was acked by the client
                    }

                    // Otherwise, not safe to remove
                    canRemove = false;
                    break;
                }

                if (canRemove)
                {
                    var lastIndex = removedEntities.Count - 1;
                    var lastElement = removedEntities[lastIndex];
                    removedEntities[lastIndex] = removedEntities[index];
                    removedEntities[index] = lastElement;

                    removedEntities.RemoveAt(lastIndex);
                    index--;//remain at same index which is now the last item
                }
            }
        }


        public void HandleEvent(RailEvent @event, RailServerPeer railServerPeer)
        {
            if (@event.TargetEntity.IsValid)
            {
                if (Entities.TryGetValue(@event.TargetEntity, out var entity))
                {
                    entity.HandleEvent(@event, railServerPeer);
                }
                else
                {
                    Debug.LogError($"Could not find entity for {@event} {@event.TargetEntity}");
                }
            }

            //todo send room event
        }

        protected void RegisterEntity(IServerEntity entity)
        {
            _entities.Add(entity.Id, entity);
            entity.Added(this);
            CallAddedEntity(entity);
        }

        protected bool RemoveEntity(IServerEntity entity)
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

    }
}
