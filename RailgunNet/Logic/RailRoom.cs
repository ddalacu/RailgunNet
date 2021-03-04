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
        private readonly Dictionary<EntityId, RailEntityBase> entities;

        protected RailRoom(RailResource resource, RailConnection connection)
        {
            Resource = resource;
            this.connection = connection;
            entities = new Dictionary<EntityId, RailEntityBase>(EntityId.CreateEqualityComparer());
            Tick = Tick.INVALID;
        }

        public RailResource Resource { get; }

        public object UserData { get; set; }

        /// <summary>
        ///     The current synchronized tick. On clients this will be the predicted
        ///     server tick. On the server this will be the authoritative tick.
        /// </summary>
        public Tick Tick { get; protected set; }

        /// <summary>
        ///     All of the entities currently added to this room.
        /// </summary>
        public IReadOnlyDictionary<EntityId, RailEntityBase> Entities => entities;

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
        public event Action<RailEntityBase> EntityRemoved;

        /// <summary>
        ///     Notifies that we removed an entity.
        /// </summary>
        public event Action<RailEntityBase> EntityAdded;

        public bool TryGet<T>(EntityId id, out T value)
            where T : RailEntityBase
        {
            if (entities.TryGetValue(id, out RailEntityBase entity))
            {
                value = entity as T;
                return true;
            }

            value = null;
            return false;
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

        protected void RegisterEntity(RailEntityBase entity)
        {
            entities.Add(entity.Id, entity);
            entity.RoomBase = this;
            EntityAdded?.Invoke(entity);
        }

        protected bool RemoveEntity(RailEntityBase entity)
        {
            if (entities.ContainsKey(entity.Id))
            {
                entities.Remove(entity.Id);
                entity.Removed();
                entity.RoomBase = null;
                // TODO: Pooling entities?

                EntityRemoved?.Invoke(entity);
                return true;
            }

            return false;
        }
    }
}
