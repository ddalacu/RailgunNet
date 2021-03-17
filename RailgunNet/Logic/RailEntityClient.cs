using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RailgunNet.Connection.Client;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using RailgunNet.Util.Pooling;
using UnityEngine;

namespace RailgunNet.Logic
{

    /// <summary>
    ///     Handy shortcut class for auto-casting the state and command.
    /// </summary>
    public abstract class RailEntityClient<TState, TCommand> : RailEntityClient<TState>
        where TState : RailState, new()
        where TCommand : RailCommand, new()
    {
        protected sealed override void WriteCommandGeneric(RailCommand toPopulate)
        {
            WriteCommand((TCommand)toPopulate);
        }

        protected sealed override void ApplyControlGeneric(RailCommand toApply)
        {
            ApplyCommand((TCommand)toApply);
        }

        /// <summary>
        ///     Populate the provided command instance.
        ///     Called on client controller.
        /// </summary>
        /// <param name="toPopulate"></param>
        [PublicAPI]
        protected abstract void WriteCommand(TCommand toPopulate);

        /// <summary>
        ///     Applies a command to this instance.
        ///     Called on controller and server.
        /// </summary>
        /// <param name="toApply"></param>
        [PublicAPI]
        protected abstract void ApplyCommand(TCommand toApply);

    }


    public interface IPreTickUpdateListener
    {
        void PreTickUpdate(Tick tick);
    }

    public interface ITickUpdateListener
    {
        void TickUpdate(Tick tick);
    }

    public interface IPostTickUpdateListener
    {
        void PostTickUpdate(Tick tick);
    }

    public interface IProduceCommands : IClientEntity
    {
        Tick LastSentCommandTick { get; set; }
        IEnumerable<RailCommand> OutgoingCommands { get; }
    }

    public static class ClientEntityExtensions
    {
        public static bool IsRemoving(this IClientEntity entity)
        {
            return entity.RemovedTick.IsValid;
        }

        public static void RaiseEvent<T>(this IClientEntity entity, Action<T> initializer, ushort attempts = 3)
            where T : RailEvent
        {
            entity.Room.RaiseEvent(initializer, entity.Id, attempts);
        }
    }

    public abstract class RailEntityClient<TState> : IProduceCommands, IPreTickUpdateListener, ITickUpdateListener
        where TState : RailState, IRailPoolable<RailState>, new()
    {

        protected IRailCommandConstruction CommandCreator { get; private set; }

        protected IRailEventConstruction EventCreator { get; private set; }

        /// <summary>
        ///     The current local state.
        /// </summary>
        public Tick RemovedTick { get; protected set; } = Tick.INVALID;

        public bool IsFrozen { get; protected set; } = true;

        public EntityId Id { get; protected set; } = EntityId.INVALID;

        /// <summary>
        ///     The current local state.
        /// </summary>
        protected TState State { get; set; }

        private RailDejitterBuffer<RailStateDelta> incomingStates;
        private readonly Queue<RailCommand> outgoingCommands;

        public Tick authTick = Tick.START;

        private bool shouldBeFrozen = true;

        public bool IsControlling { get; set; }
        public RailClientRoom Room { get; private set; }

        public IEnumerable<RailCommand> OutgoingCommands => outgoingCommands;

        public Tick
            LastSentCommandTick
        {
            get;
            set;
        } // The last local tick we sent our commands to the server

        public bool ProducesCommands { get; protected set; } = false;

        public Tick nextTick { get; private set; }


        private TState _authState;

        private TState _nextState;

        /// <summary>
        ///     Returns the current dejittered authoritative state from the server.
        ///     Will return null if the entity is locally controlled (use State).
        /// </summary>
        public TState AuthState
        {
            get
            {
                // Not valid if we're controlling
                if (IsControlling)
                    return null;
                return _authState;
            }
        }


        /// <summary>
        ///     Returns the current dejittered authoritative state from the server.
        ///     Will return null if the entity is locally controlled (use State).
        /// </summary>
        public TState NextAuthState
        {
            get
            {
                // Not valid if we're controlling
                if (IsControlling)
                    return null;

                return _nextState;
            }
        }


        protected RailEntityClient()
        {
            outgoingCommands = new Queue<RailCommand>();
            State = new TState();
            _authState = new TState();
            _nextState = new TState();
        }

        #region Pooling

        IRailMemoryPool<IEntity> IRailPoolable<IEntity>.OwnerPool { get; set; }


        public virtual void Reset()
        {
            CommandCreator = null;
            Id = EntityId.INVALID;

            Room = null;
            LastSentCommandTick = Tick.START;
            IsFrozen = true; // Entities start frozen on client
            shouldBeFrozen = true;

            if (incomingStates != null)
                incomingStates.Clear();
            RailPool.DrainQueue(outgoingCommands);

            authTick = Tick.START;

            State.Reset();
            _authState.Reset();
            _nextState.Reset();
        }

        void IRailPoolable<IEntity>.Allocated()
        {
        }

        #endregion


        /// <summary>
        ///     Applies a command to this instance.
        ///     Called on controller and server.
        /// </summary>
        /// <param name="toApply"></param>
        protected virtual void ApplyControlGeneric(RailCommand toApply)
        {
        }


        public void Initialize(EntityId id, RailClient client)
        {
            Id = id;

            if (incomingStates == null)
            {
                incomingStates = new RailDejitterBuffer<RailStateDelta>(
                    RailConfig.DEJITTER_BUFFER_LENGTH,
                    client.RemoteSendRate);
            }
            else
            {
                RailDebug.Assert(incomingStates.Divisor == client.RemoteSendRate);
            }
        }

        public float ComputeInterpolation(Tick tick, float tickDeltaTime, float timeSinceTick)
        {
            if (nextTick == Tick.INVALID)
            {
                throw new InvalidOperationException("Next state is invalid");
            }

            float curTime = authTick.ToTime(tickDeltaTime);
            float nextTime = nextTick.ToTime(tickDeltaTime);
            float showTime = (tick).ToTime(tickDeltaTime) + timeSinceTick;

            float progress = showTime - curTime;
            float span = nextTime - curTime;
            if (span <= 0.0f) return 0.0f;
            return progress / span;
        }

        public void TickUpdate(Tick localTick)
        {
            SetFreeze(shouldBeFrozen);
            if (IsFrozen)
            {
                UpdateFrozen();
            }
            else
            {
                if (IsControlling == false)
                {
                    UpdateProxy(localTick);
                }
                else if (ProducesCommands)
                {
                    UpdateControlled(localTick);
                    UpdatePredicted();
                }
            }
        }

        public bool HasReadyState(Tick tick)
        {
            return incomingStates.GetLatestAt(tick) != null;
        }

        /// <summary>
        ///     Applies the initial creation delta.
        /// </summary>
        public void PrimeState(RailStateDelta delta)
        {
            RailDebug.Assert(delta.IsFrozen == false);
            RailDebug.Assert(delta.IsRemoving == false);
            RailDebug.Assert(delta.HasImmutableData);
            _authState.ApplyDelta(delta);
            State.OverwriteFrom(_authState);
        }

        private Tick _latestDeltaTick;


        /// <summary>
        ///     Returns true iff we stored the delta. False if it will leak.
        /// </summary>
        public bool ReceiveDelta(RailStateDelta delta)
        {
            if (delta.IsFrozen)
            {
                // Frozen deltas have no state data, so we need to treat them
                // separately when doing checks based on state content
                return incomingStates.Store(delta);
            }

            if (delta.IsRemoving)
                RemovedTick = delta.RemovedTick;

            //delta.Tick += 7;//change this

            _latestDeltaTick = delta.Tick;

            return incomingStates.Store(delta);
        }

        /// <summary>
        ///     Frees all outgoing commands that are older than the given Tick.
        /// </summary>
        /// <param name="ackTick"></param>
        private void FreeCommandsUpTo(Tick ackTick)
        {
            if (ackTick.IsValid == false) return;

            while (outgoingCommands.Count > 0)
            {
                RailCommand command = outgoingCommands.Peek();
                if (command.ClientTick > ackTick) break;
                RailPool.Free(outgoingCommands.Dequeue());
            }
        }

        private void UpdateControlled(Tick localTick)
        {
            RailDebug.Assert(IsControlling);

            if (outgoingCommands.Count < RailConfig.COMMAND_BUFFER_COUNT)
            {
                RailCommand command = CommandCreator.CreateCommand();

                command.ClientTick = localTick;
                command.IsNewCommand = true;

                WriteCommandGeneric(command);
                outgoingCommands.Enqueue(command);
            }
        }

        public void InitState(RailResource resource)
        {
            CommandCreator = resource;
            EventCreator = resource;
        }


        protected void ClearCommands()
        {
            outgoingCommands.Clear();
            LastSentCommandTick = Tick.START;
        }


        public void CheckIfControlling(RailStateDelta delta)
        {
            // Can't infer anything if the delta is an empty frozen update
            if (delta.IsFrozen)
                return;

            if (delta.IsRemoving)
            {
                IsControlling = false;
                Room.ControlledEntities.Remove(this);
            }
            else
            if (delta.HasControllerData)
            {
                if (IsControlling == false)
                {
                    IsControlling = true;
                    Room.ControlledEntities.Add(this);
                }
            }
            else
            {
                if (IsControlling)
                {
                    IsControlling = false;
                    Room.ControlledEntities.Remove(this);
                }
            }
        }

        /// <summary>
        ///     Updates the local instance of the authoritative state.
        /// </summary>
        private void UpdateAuthoritativeState(Tick localTick)
        {
            // Apply all un-applied deltas to the auth state
            var toApply = incomingStates.GetRangeAndNext(
                authTick,
                this.IsRemoving() ? RemovedTick : localTick,
                out RailStateDelta next);

            //Debug.Log(next);

            RailStateDelta lastDelta = null;
            foreach (RailStateDelta delta in toApply)
            {
                if (delta.IsFrozen == false)
                    _authState.ApplyDelta(delta);

                shouldBeFrozen = delta.IsFrozen;
                authTick = delta.Tick;
                lastDelta = delta;
            }

            if (this.IsRemoving() == false &&
                lastDelta != null)
            {
                // Update the control status based on the most recent delta
                CheckIfControlling(lastDelta);
            }

            //If there was a next state, update the next state
            bool canGetNext = shouldBeFrozen == false;
            if (canGetNext && next != null &&
                next.IsFrozen == false)
            {
                _nextState.OverwriteFrom(_authState);
                _nextState.ApplyDelta(next);
                nextTick = next.Tick;
            }
            else
            {
                nextTick = Tick.INVALID;
            }
        }

        /// <summary>
        ///     Updates the local state with all outgoing commands (if there are any).
        /// </summary>
        private void UpdatePredicted()
        {
            // Bring the main state up to the latest (apply all deltas)
            var deltas = incomingStates.GetRangeStartingFrom(authTick);

            RailStateDelta lastAppliedDelta = null;
            foreach (RailStateDelta delta in deltas)
            {
                // It's possible the state is null if we lost control
                // and then immediately went out of scope of the entity
                if (delta.State == null) break;
                if (delta.HasControllerData == false) break;
                State.ApplyDelta(delta);
                lastAppliedDelta = delta;
            }

            if (lastAppliedDelta != null)
            {
                FreeCommandsUpTo(lastAppliedDelta.CommandAck);

                Revert(lastAppliedDelta.CommandAck);
            }

            // Forward-simulate
            foreach (RailCommand command in outgoingCommands)
            {
                ApplyControlGeneric(command);
                command.IsNewCommand = false;
            }
        }

        private void SetFreeze(bool isFrozen)
        {
            if (IsFrozen == false && isFrozen)
            {
                OnFrozen();
            }
            else if (IsFrozen && isFrozen == false) OnUnfrozen();

            IsFrozen = isFrozen;
        }

        #region Lifecycle and Loop
        public void PreTickUpdate(Tick localTick)
        {
            UpdateAuthoritativeState(localTick);
            State.OverwriteFrom(_authState);
        }

        public void Removed()
        {
            // Set the final auth state before removing
            UpdateAuthoritativeState(RemovedTick);
            State.OverwriteFrom(_authState);
            OnRemoved();
            Room = null;
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

        #endregion

        #region Override Functions
        /// <summary>
        ///     Called during UpdatePredicted after updating the StateBase.
        ///     Called on client controller.
        /// </summary>
        protected virtual void Revert(Tick ackTick)
        {
        }

        /// <summary>
        ///     Populate the provided command instance.
        ///     Called on client controller.
        /// </summary>
        /// <param name="toPopulate"></param>
        protected virtual void WriteCommandGeneric(RailCommand toPopulate)
        {
        }

        /// <summary>
        ///     Called on every tick for frozen entities.
        ///     Called on client for all client entities.
        /// </summary>
        protected virtual void UpdateFrozen()
        {
        }

        /// <summary>
        ///     Update for non-controlled entities.
        ///     Called on non-controller client.
        /// </summary>
        protected virtual void UpdateProxy(Tick localTick)
        {
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

        public void RaiseEvent<T>(Action<T> initializer, ushort attempts = 3)
            where T : RailEvent
        {
            Room.RaiseEvent<T>(initializer, Id, attempts);
        }

        #endregion

        public virtual void HandleEvent(RailEvent @event)
        {

        }

        public void Added(RailClientRoom room)
        {
            Room = room;
            OnAdded();
        }
    }
}
