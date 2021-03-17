using RailgunNet.Factory;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic.Wrappers
{
    public class RailStateDelta : IRailPoolable<RailStateDelta>, IRailTimedValue
    {
        private RailState state;

        public RailStateDelta()
        {
            Reset();
        }

        public Tick Tick { get; set; }

        public EntityId EntityId { get; private set; }

        public RailState State => state;
        public bool IsFrozen { get; private set; }

        public bool HasControllerData => state.HasControllerData;
        public bool HasImmutableData => state.HasImmutableData;
        public bool IsRemoving => RemovedTick.IsValid;
        public Tick RemovedTick { get; private set; }

        public Tick CommandAck { get; private set; }

        Tick IRailTimedValue.Tick => Tick;

        public static RailStateDelta CreateFrozen(
            IRailStateConstruction stateCreator,
            Tick tick,
            EntityId entityId)
        {
            RailStateDelta delta = stateCreator.CreateDelta();
            delta.Initialize(tick, entityId, null,Tick.INVALID, Tick.INVALID, true);
            return delta;
        }

        public void Initialize(
            Tick tick,
            EntityId entityId,
            RailState state,
            Tick commandAck,
            Tick removedTick,
            bool isFrozen)
        {
            Tick = tick;
            EntityId = entityId;
            this.state = state;
            CommandAck = commandAck;
            RemovedTick = removedTick;
            IsFrozen = isFrozen;
        }

        private void Reset()
        {
            Tick = Tick.INVALID;
            EntityId = EntityId.INVALID;
            RailPool.SafeReplace(ref state, null);
            IsFrozen = false;
        }

        #region Pooling
        IRailMemoryPool<RailStateDelta> IRailPoolable<RailStateDelta>.OwnerPool { get; set; }

        void IRailPoolable<RailStateDelta>.Reset()
        {
            Reset();
        }

        void IRailPoolable<RailStateDelta>.Allocated()
        {
        }
        #endregion
    }
}
