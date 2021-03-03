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
using System.Linq;
using JetBrains.Annotations;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RailgunNet.Util;

namespace RailgunNet.Connection.Server
{
    [OnlyIn(Component.Server)]
    public class RailServerRoom : RailRoom
    {
        /// <summary>
        ///     All client controllers involved in this room.
        ///     Does not include the server's controller.
        /// </summary>
        private readonly List<RailServerPeer> clients;

        /// <summary>
        ///     Used for creating new entities and assigning them unique ids.
        /// </summary>
        private EntityId nextEntityId = EntityId.START;

        private List<RailEntityServer> removeBuffer;// Pre-allocated removal list

        private List<RailEntityServer> updateBuffer; // Pre-allocated update list

        /// <summary>
        ///     Entities that have been removed or are about to be.
        /// </summary>
        private readonly List<RailEntityServer> removedEntities = new List<RailEntityServer>();

        public RailServerRoom(RailResource resource, RailServer server) : base(resource, server)
        {
            updateBuffer = new List<RailEntityServer>();
            removeBuffer = new List<RailEntityServer>();

            clients = new List<RailServerPeer>();
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
            where T : RailEntityServer
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
            if (TryGet(id, out RailEntityBase entity))
            {
                MarkForRemoval(entity);
            }
        }

        /// <summary>
        ///     Marks an entity for removal from the room and presumably destruction.
        ///     This is deferred until the next frame.
        /// </summary>
        public void MarkForRemoval(RailEntityBase entity)
        {
            if (entity.IsRemoving == false)
            {
                RailEntityServer serverEntity = entity as RailEntityServer;
                if (serverEntity == null)
                {
                    throw new ArgumentNullException(
                        nameof(entity),
                        $"unexpected type of entity to remove: {entity}");
                }

                serverEntity.MarkForRemoval();

                removedEntities.Add(serverEntity);
            }
        }

        /// <summary>
        ///     Queues an event to broadcast to all present clients.
        ///     Notice that due to the internal object pooling, the event will be cloned and managed
        ///     internally in each client peer. The <paramref name="evnt" /> will be freed if
        ///     <paramref name="freeWhenDone" /> is true, otherwise the caller is responsible to
        ///     free the memory.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="attempts"></param>
        /// <param name="freeWhenDone"></param>
        public void BroadcastEvent(
            [NotNull] RailEvent evnt,
            ushort attempts = 3,
            bool freeWhenDone = true)
        {
            foreach (RailPeer client in clients)
            {
                client.SendEvent(evnt, attempts, true);
            }

            if (freeWhenDone)
            {
                evnt.Free();
            }
        }

        public void AddClient(RailServerPeer client)
        {
            var index = clients.IndexOf(client);

            if (index == -1)
            {
                clients.Add(client);
                ClientJoined?.Invoke(client);
            }
        }

        public void RemoveClient(RailServerPeer client)
        {
            if (clients.Remove(client))
            {
                ClientLeft?.Invoke(client);
            }
        }

        public void UpdateClients()
        {
            foreach (RailServerPeer client in clients)
            {
                client.Update(Tick);
            }
        }

        public void ServerUpdate()
        {
            Tick = Tick.GetNext();
            OnPreRoomUpdate(Tick);

            // Collect the entities in the priority order and
            // separate them out for either update or removal
            foreach (var railEntityBase in Entities.Values)
            {
                var entity = (RailEntityServer)railEntityBase;

                if (entity.ShouldRemove)
                {
                    removeBuffer.Add(entity);
                }
                else
                {
                    updateBuffer.Add(entity);
                }
            }

            // Wave 0: Remove all sunsetted entities

            foreach (var railEntityServer in removeBuffer)
                RemoveEntity(railEntityServer);
            removeBuffer.Clear();

            int updateBufferCount = updateBuffer.Count;

            // Wave 1: Start/initialize all entities
            for (var index = 0; index < updateBufferCount; index++)
                updateBuffer[index].PreUpdate();

            // Wave 2: Update all entities
            for (var index = 0; index < updateBufferCount; index++)
                updateBuffer[index].ServerUpdate();

            // Wave 3: Post-update all entities
            for (var index = 0; index < updateBufferCount; index++)
                updateBuffer[index].PostUpdate();

            updateBuffer.Clear();

            OnPostRoomUpdate(Tick);
        }

        public void StoreStates()
        {
            foreach (RailEntityServer entity in Entities.Values)
            {
                entity.StoreRecord(Resource);
            }
        }

        private T CreateEntity<T>()
            where T : RailEntityServer
        {
            T entity = RailEntityServer.Create<T>(Resource);
            entity.AssignId(nextEntityId);
            nextEntityId = nextEntityId.GetNext();
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

                foreach (RailServerPeer peer in clients)
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


        /// <summary>
        ///     Packs and sends a server-to-client packet to each peer.
        /// </summary>
        public void BroadcastPackets()
        {
            foreach (var clientPeer in clients)
            {
                clientPeer.SendPacket(Tick, Entities.Values.Select(e => e as RailEntityServer), removedEntities);
            }
        }
    }
}
