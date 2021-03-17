using System;
using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.System.Types;

namespace RailgunNet.Logic
{
    public abstract class RailRoom<T> where T : IEntity
    {
        protected readonly Dictionary<EntityId, T> _entities = new Dictionary<EntityId, T>(EntityId.ComparerInstance);

        /// <summary>
        ///     All of the entities currently added to this room.
        /// </summary>
        public IReadOnlyDictionary<EntityId, T> Entities => _entities;

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
        public event Action<T> EntityRemoved;

        /// <summary>
        ///     Notifies that we removed an entity.
        /// </summary>
        public event Action<T> EntityAdded;

        public bool TryGet<TEntity>(EntityId id, out TEntity value) where TEntity : class, T
        {
            if (_entities.TryGetValue(id, out var entity))
            {
                value = entity as TEntity;
                return true;
            }

            value = null;
            return false;
        }

        protected void CallPreRoomUpdate(Tick tick)
        {
            PreRoomUpdate?.Invoke(tick);
        }

        protected void CallPostRoomUpdate(Tick tick)
        {
            PostRoomUpdate?.Invoke(tick);
        }

        protected void CallAddedEntity(T entity)
        {
            EntityAdded?.Invoke(entity);
        }

        protected void CallRemovedEntity(T entity)
        {
            EntityRemoved?.Invoke(entity);
        }

    }

    public static class RailRoomExtensions
    {
        public static bool TryGetFirstEntityOfType<TRoomBase, T>(this RailRoom<TRoomBase> room, out T result)
            where T : IEntity
            where TRoomBase : IEntity
        {
            foreach (var roomEntity in room.Entities.Values)
            {
                if (roomEntity is T casted)
                {
                    result = casted;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
