using RailgunNet.Connection.Client;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic
{
    public abstract class SimpleClientEntity<T> : IClientEntity, ITickUpdateListener where T : RailState, IRailPoolable<RailState>, new()
    {
        private bool _shouldBeFrozen;

        public Tick RemovedTick { get; private set; }

        public bool IsFrozen { get; private set; } = true;

        public EntityId Id { get; private set; }

        /// <summary>
        ///     The current local state.
        /// </summary>
        protected T State { get; private set; }

        public RailClientRoom Room { get; private set; }

        public SimpleClientEntity()
        {
            State = new T();
        }

        public void Initialize(EntityId id, RailClient client)
        {
            Id = id;
        }

        public bool HasReadyState(Tick serverTick)
        {
            return true;
        }

        public void TickUpdate(Tick localTick)
        {
            if (IsFrozen == false &&
                _shouldBeFrozen)
            {
                OnFrozen();
            }
            else
            {
                if (IsFrozen && _shouldBeFrozen == false)
                    OnUnfrozen();
            }

            IsFrozen = _shouldBeFrozen;
        }

        /// <summary>
        ///     Applies the initial creation delta.
        /// </summary>
        public void PrimeState(RailStateDelta delta)
        {
            RailDebug.Assert(delta.IsFrozen == false);
            RailDebug.Assert(delta.IsRemoving == false);
            RailDebug.Assert(delta.HasImmutableData);
            State.ApplyDelta(delta);
        }

        /// <summary>
        ///     Returns true iff we stored the delta. False if it will leak.
        /// </summary>
        public bool ReceiveDelta(RailStateDelta delta)
        {
            if (delta.IsFrozen == false)
                State.ApplyDelta(delta);

            if (delta.IsRemoving)
                RemovedTick = delta.RemovedTick;
            _shouldBeFrozen = delta.IsFrozen;
            return false;
        }


        public IRailMemoryPool<IEntity> OwnerPool { get; set; }

        public void Allocated()
        {

        }

        public void InitState(RailResource resource)
        {

        }

        public virtual void Reset()
        {
            RailDebug.Assert(Room == null);
            IsFrozen = true; // Entities start frozen on client
            _shouldBeFrozen = true;
            State.Reset();
        }


        /// <summary>
        ///     When the entity was removed from a room.
        ///     Called on all.
        /// </summary>
        public virtual void Removed()
        {
            Room = null;
        }

        /// <summary>
        ///     When the entity was added to a room.
        ///     Called on all.
        /// </summary>
        public virtual void Added(RailClientRoom room)
        {
            Room = room;
        }

        /// <summary>
        ///     When an entity is frozen.
        ///     Called on client.
        /// </summary>
        protected virtual void OnFrozen()
        {
        }

        /// <summary>
        ///     When an entity is unfrozen.
        ///     Called on client.
        /// </summary>
        protected virtual void OnUnfrozen()
        {
        }

        public virtual void HandleEvent(RailEvent @event)
        {

        }
    }
}