using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic
{
    public abstract class SimpleServerEntity<T> : IServerEntity, IPreTickUpdateListener
        where T : RailState, IRailPoolable<RailState>, new()
    {
        private readonly RailQueueBuffer<RailStateRecord> _outgoingStates;

        // The controller at the time of entity removal
        private RailServerPeer _priorController;

        /// <summary>
        ///     The current local state.
        /// </summary>
        public T State { get; private set; }

        public Tick RemovedTick { get; protected set; }

        public bool IsFrozen { get; protected set; }

        public EntityId Id { get; protected set; }


        public RailServerRoom Room { get; private set; }

        public RailServerPeer Controller { get; private set; }

        private bool _deferNotifyControllerChanged;

        protected SimpleServerEntity()
        {
            // We use no divisor for storing commands because commands are sent in
            // batches that we can use to fill in the holes between send ticks
            _outgoingStates = new RailQueueBuffer<RailStateRecord>(RailConfig.DEJITTER_BUFFER_LENGTH);
            State = new T();
        }

        public virtual void Reset()
        {
            IsFrozen = false;
            Id = EntityId.INVALID;

            Room = null;
            Controller = null;
            _deferNotifyControllerChanged = true; // We always notify a controller change at start

            _outgoingStates.Clear();
            State.Reset();
        }

        #region Pooling
        IRailMemoryPool<IEntity> IRailPoolable<IEntity>.OwnerPool { get; set; }

        void IRailPoolable<IEntity>.Reset()
        {
            Reset();
        }

        void IRailPoolable<IEntity>.Allocated()
        {
        }
        #endregion

        public virtual void InitState(RailResource resource)
        {

        }

        public void Initialize(EntityId id)
        {
            this.Id = id;
        }

        public virtual void MarkForRemoval()
        {
            // We'll remove on the next tick since we're probably 
            // already mid-way through evaluating this tick
            RemovedTick = Room.Tick + 1;
        }

        public virtual void BeforeStore()
        {

        }

        public void StoreRecord(IRailStateConstruction stateCreator)
        {
            BeforeStore();

            if (_outgoingStates.Latest != null)
            {
                var latest = _outgoingStates.Latest.State;

                var dataChanged =
                    State.DataSerializer.CompareMutableData(latest.DataSerializer) > 0 ||
                    State.DataSerializer.IsControllerDataEqual(latest.DataSerializer) == false;

                if (dataChanged == false)
                    return;
            }

            var record = stateCreator.CreateRecord();
            record.Overwrite(stateCreator, Room.Tick, State);

            _outgoingStates.Store(record);
        }

        public RailStateDelta ProduceDelta(
            IRailStateConstruction stateCreator,
            Tick basisTick,
            RailServerPeer destination,
            bool forceAllMutable)
        {
            // Flags for special data modes
            bool includeControllerData =
                destination == Controller || destination == _priorController;
            bool includeImmutableData = basisTick.IsValid == false;

            return RailStateDeltaFactory.Create(
                stateCreator,
                Id,
                State,
                _outgoingStates.LatestFrom(basisTick),
                includeControllerData,
                includeImmutableData,
                Room.Tick,
                RemovedTick,
                forceAllMutable);
        }

        public virtual void Removed()
        {
            // Automatically revoke control but keep a history for 
            // sending the final controller data to the client.
            if (Controller != null)
            {
                _priorController = Controller;
                Controller.RevokeControlInternal(this);
                NotifyControllerChanged();
            }

            Room = null;
        }

        public virtual void Added(RailServerRoom room)
        {
            Room = room;
        }

        public void AssignController(RailServerPeer controller)
        {
            if (Controller != controller)
            {
                Controller = controller;
                _deferNotifyControllerChanged = true;
            }
        }

        protected void NotifyControllerChanged()
        {
            if (_deferNotifyControllerChanged)
                OnControllerChanged();
            _deferNotifyControllerChanged = false;
        }

        protected virtual void OnControllerChanged()
        {

        }

        public virtual void PreTickUpdate(Tick localTick)
        {
            NotifyControllerChanged();
        }


        public virtual void HandleEvent(RailEvent railEvent, RailServerPeer senderPeer)
        {

        }
    }
}