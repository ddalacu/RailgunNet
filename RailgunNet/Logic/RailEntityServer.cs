using JetBrains.Annotations;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;
using UnityEngine;

namespace RailgunNet.Logic
{

    /// <summary>
    ///     Handy shortcut class for auto-casting the state and command.
    /// </summary>
    public abstract class RailEntityServer<TState, TCommand> : RailEntityServer<TState>
        where TState : RailState, new()
        where TCommand : RailCommand
    {
        /// <summary>
        ///     Applies a command to this instance.
        ///     Called on controller and server.
        /// </summary>
        /// <param name="toApply"></param>
        [PublicAPI]
        protected abstract void ApplyCommand(TCommand toApply);

        protected sealed override void ApplyControlGeneric(RailCommand toApply)
        {
            ApplyCommand((TCommand)toApply);
        }
    }


    public abstract class RailEntityServer<TState> : IServerCommandedEntity, IPreTickUpdateListener, ITickUpdateListener
        where TState : RailState, IRailPoolable<RailState>, new()
    {
        private readonly RailDejitterBuffer<RailCommand> _incomingCommands;
        private readonly RailQueueBuffer<RailStateRecord> _outgoingStates;

        // The controller at the time of entity removal
        private RailServerPeer priorController;

        public RailServerRoom Room { get; private set; }

        public RailServerPeer Controller { get; private set; }

        private bool deferNotifyControllerChanged = true;

        protected IRailCommandConstruction CommandCreator { get; private set; }

        protected IRailEventConstruction EventCreator { get; private set; }

        /// <summary>
        ///     The current local state.
        /// </summary>
        protected TState State { get; set; }

        public Tick RemovedTick { get; protected set; }

        public bool IsFrozen { get; protected set; } = false;

        public EntityId Id { get; protected set; } = EntityId.INVALID;


        public RailEntityServer()
        {
            // We use no divisor for storing commands because commands are sent in
            // batches that we can use to fill in the holes between send ticks
            _incomingCommands = new RailDejitterBuffer<RailCommand>(RailConfig.DEJITTER_BUFFER_LENGTH);
            _outgoingStates = new RailQueueBuffer<RailStateRecord>(RailConfig.DEJITTER_BUFFER_LENGTH);
            State = new TState();
        }

        #region Pooling

        IRailMemoryPool<IEntity> IRailPoolable<IEntity>.OwnerPool { get; set; }

        public virtual void Reset()
        {
            IsFrozen = false;
            CommandCreator = null;
            Id = EntityId.INVALID;

            Room = null;
            Controller = null;
            deferNotifyControllerChanged = true; // We always notify a controller change at start

            _outgoingStates.Clear();
            _incomingCommands.Clear();

            State.Reset();
        }

        void IRailPoolable<IEntity>.Allocated()
        {
        }
        #endregion

        public virtual void InitState(RailResource resource)
        {
            CommandCreator = resource;
            EventCreator = resource;
        }

        /// <summary>
        ///     Applies a command to this instance.
        ///     Called on controller and server.
        /// </summary>
        /// <param name="toApply"></param>
        protected virtual void ApplyControlGeneric(RailCommand toApply)
        {
        }

        public void Initialize(EntityId id)
        {
            this.Id = id;
        }

        public void MarkForRemoval()
        {
            // We'll remove on the next tick since we're probably 
            // already mid-way through evaluating this tick
            RemovedTick = Room.Tick + 1;

            // Notify our inheritors that we are being removed next tick
            OnSunset();
        }


        public virtual void TickUpdate(Tick tick)
        {
            UpdateAuthoritative();

            if (Controller != null)
            {
                var latest = GetLatestCommand(tick);

                if (latest != null)
                {
                    var remaining = _latestTick - latest.ClientTick;

                    //Debug.Log("Remaining " + remaining);

                    // Debug.Log(tick);
                    //Debug.Log(latest.ClientTick);


                    // Debug.Assert(tick == latest.ClientTick);
                    Debug.Assert(latest.IsNewCommand);

                    ApplyControlGeneric(latest);
                    latest.IsNewCommand = false;
                }
                else
                {
                    var command = CommandCreator.CreateCommand();
                    command.ClientTick = tick;
                    command.IsNewCommand = false;

                    ApplyControlGeneric(command);
                    Debug.LogError("No command!");
                }
            }
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
                destination == Controller || destination == priorController;
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

        public void Removed()
        {
            // Automatically revoke control but keep a history for 
            // sending the final controller data to the client.
            if (Controller != null)
            {
                priorController = Controller;
                Controller.RevokeControlInternal(this);
                NotifyControllerChanged();
            }

            OnRemoved();
            Room = null;
        }

        public void Added(RailServerRoom room)
        {
            Room = room;
            OnAdded();
        }

        /// <summary>
        ///     When the entity was added to a room.
        ///     Called on all.
        /// </summary>
        protected virtual void OnAdded()
        {
        }

        /// <summary>
        ///     When the entity was removed from a room.
        ///     Called on all.
        /// </summary>
        protected virtual void OnRemoved()
        {
        }



        public void AssignController(RailServerPeer controller)
        {
            if (Controller != controller)
            {
                Controller = controller;
                ClearCommands();
                deferNotifyControllerChanged = true;
            }
        }

        protected void NotifyControllerChanged()
        {
            if (deferNotifyControllerChanged)
                OnControllerChanged();
            deferNotifyControllerChanged = false;
        }

        protected virtual void OnControllerChanged()
        {

        }

        public virtual void PreTickUpdate(Tick localTick)
        {
            NotifyControllerChanged();
        }


        protected void ClearCommands()
        {
            _incomingCommands.Clear();
        }

        private Tick _latestTick;

        public void ReceiveCommand(RailCommand command)
        {

            if (_incomingCommands.Store(command))
            {
                if (_latestTick == Tick.INVALID)
                    _latestTick = command.ClientTick;
                else
                if (command.ClientTick > _latestTick)
                    _latestTick = command.ClientTick;

                command.IsNewCommand = true;
            }
            else
            {
                RailPool.Free(command);
            }
        }

        private RailCommand GetLatestCommand(Tick tick)
        {
            if (tick.IsValid == false)
                return null;

            if (Controller != null)
                return _incomingCommands.GetLatestAt(tick);

            return null;
        }

        /// <summary>
        ///     Called first in an update, before processing a command. Clients will obey
        ///     to this state for all non-controlled entities.
        ///     Called on server.
        /// </summary>
        protected virtual void UpdateAuthoritative()
        {
        }

        /// <summary>
        ///     When the entity will be removed on the next tick.
        ///     Called on server.
        /// </summary>
        protected virtual void OnSunset()
        {
        } // Called on server


        public virtual void HandleEvent(RailEvent railEvent, RailServerPeer senderPeer)
        {

        }

    }
}
