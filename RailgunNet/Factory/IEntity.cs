using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Factory
{
    public interface IEntity : IRailPoolable<IEntity>
    {
        EntityId Id { get; }
        Tick RemovedTick { get; }

        void InitState(RailResource resource);
    }


    public static class EntityExtensions
    {
        /// <summary>
        ///     Whether or not this entity should be removed from a room this tick.
        /// </summary>
        public static bool ShouldRemove(this IEntity entity, Tick roomTick)
        {
            return entity.RemovedTick.IsValid &&
                   entity.RemovedTick <= roomTick;
        }


        public static bool IsRemoving(this IEntity entity)
        {
            return entity.RemovedTick.IsValid;
        }
    }
}