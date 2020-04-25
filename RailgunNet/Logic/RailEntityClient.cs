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

namespace RailgunNet.Logic
{
    public interface IRailEntityClient
    {
        bool IsControlled { get; }

        /// <summary>
        ///     The tick of the last authoritative state.
        /// </summary>
        Tick AuthTick { get; }

        /// <summary>
        ///     The tick of the next authoritative state. May be invalid.
        /// </summary>
        Tick NextTick { get; }

        /// <summary>
        ///     Returns the number of ticks ahead we are, for extrapolation.
        ///     Note that this does not take client-side prediction into account.
        /// </summary>
        int TicksAhead { get; }

        float ComputeInterpolation(float tickDeltaTime, float timeSinceTick);
    }

    /// <summary>
    ///     Handy shortcut class for auto-casting the state.
    /// </summary>
    public interface IRailEntityClient<out TState> : IRailEntityClient
        where TState : RailState, new()
    {
        TState State { get; }
        TState AuthState { get; }
        TState NextState { get; }
    }

    public abstract class RailEntityClient : RailEntity, IRailEntityClient
    {
        private readonly RailDejitterBuffer<RailStateDelta> incomingStates;
        private readonly Queue<RailCommand> outgoingCommands;

        private Tick authTick;
        private Tick nextTick;
        private bool shouldBeFrozen;

        protected RailEntityClient()
        {
            incomingStates = new RailDejitterBuffer<RailStateDelta>(
                RailConfig.DEJITTER_BUFFER_LENGTH,
                RailConfig.SERVER_SEND_RATE);
            outgoingCommands = new Queue<RailCommand>();

            Reset();
        }

        protected abstract RailState AuthStateBase { get; set; }
        protected abstract RailState NextStateBase { get; set; }
        public IEnumerable<RailCommand> OutgoingCommands => outgoingCommands;

        public Tick
            LastSentCommandTick
        {
            get;
            set;
        } // The last local tick we sent our commands to the server

        public bool IsControlled => Controller != null;

        /// <summary>
        ///     The tick of the last authoritative state.
        /// </summary>
        public Tick AuthTick => authTick;

        /// <summary>
        ///     The tick of the next authoritative state. May be invalid.
        /// </summary>
        public Tick NextTick => nextTick;

        /// <summary>
        ///     Returns the number of ticks ahead we are, for extrapolation.
        ///     Note that this does not take client-side prediction into account.
        /// </summary>
        public int TicksAhead => Room.Tick - authTick;

        public float ComputeInterpolation(float tickDeltaTime, float timeSinceTick)
        {
            if (nextTick == Tick.INVALID)
            {
                throw new InvalidOperationException("Next state is invalid");
            }

            float curTime = authTick.ToTime(tickDeltaTime);
            float nextTime = nextTick.ToTime(tickDeltaTime);
            float showTime = Room.Tick.ToTime(tickDeltaTime) + timeSinceTick;

            float progress = showTime - curTime;
            float span = nextTime - curTime;
            if (span <= 0.0f) return 0.0f;
            return progress / span;
        }

        public void ClientUpdate(Tick localTick)
        {
            SetFreeze(shouldBeFrozen);
            if (IsFrozen)
            {
                UpdateFrozen();
            }
            else
            {
                if (Controller == null)
                {
                    UpdateProxy();
                }
                else
                {
                    nextTick = Tick.INVALID;
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
            AuthStateBase.ApplyDelta(delta);
        }

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
            else
            {
                if (delta.IsRemoving) RemovedTick = delta.RemovedTick;
                return incomingStates.Store(delta);
            }
        }

        /// <summary>
        ///     Frees all outgoing commands that are older than the given Tick.
        /// </summary>
        /// <param name="ackTick"></param>
        private void FreeExpiredCommands(Tick ackTick)
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
            RailDebug.Assert(Controller != null);
            if (outgoingCommands.Count < RailConfig.COMMAND_BUFFER_COUNT)
            {
                RailCommand command = CommandCreator.CreateCommand();

                command.ClientTick = localTick;
                command.IsNewCommand = true;

                WriteCommandGeneric(command);
                outgoingCommands.Enqueue(command);
            }
        }

        protected sealed override void InitState(
            IRailStateConstruction creator,
            RailState initialState)
        {
            base.InitState(creator, initialState);
            AuthStateBase = StateBase.Clone(creator);
            NextStateBase = StateBase.Clone(creator);
        }

        protected sealed override void Reset()
        {
            base.Reset();

            LastSentCommandTick = Tick.START;
            IsFrozen = true; // Entities start frozen on client
            shouldBeFrozen = true;

            incomingStates.Clear();
            RailPool.DrainQueue(outgoingCommands);

            authTick = Tick.START;
            nextTick = Tick.INVALID;

            ResetStates();
            OnReset();
        }

        private void ResetStates()
        {
            if (StateBase != null) RailPool.Free(StateBase);
            if (AuthStateBase != null) RailPool.Free(AuthStateBase);
            if (NextStateBase != null) RailPool.Free(NextStateBase);

            StateBase = null;
            AuthStateBase = null;
            NextStateBase = null;
        }

        protected override void ClearCommands()
        {
            outgoingCommands.Clear();
            LastSentCommandTick = Tick.START;
        }

        private void ApplyAuthoritativeState()
        {
            // Apply all un-applied deltas to the auth state
            IEnumerable<RailStateDelta> toApply = incomingStates.GetRangeAndNext(
                authTick,
                Room.Tick,
                out RailStateDelta next);

            RailStateDelta lastDelta = null;
            foreach (RailStateDelta delta in toApply)
            {
                if (delta.IsFrozen == false) AuthStateBase.ApplyDelta(delta);
                shouldBeFrozen = delta.IsFrozen;
                authTick = delta.Tick;
                lastDelta = delta;
            }

            if (lastDelta != null)
            {
                // Update the control status based on the most recent delta
                (Room as RailClientRoom).RequestControlUpdate(this, lastDelta);
            }

            // If there was a next state, update the next state
            bool canGetNext = shouldBeFrozen == false;
            if (canGetNext && next != null && next.IsFrozen == false)
            {
                NextStateBase.OverwriteFrom(AuthStateBase);
                NextStateBase.ApplyDelta(next);
                nextTick = next.Tick;
            }
            else
            {
                nextTick = Tick.INVALID;
            }
        }

        private void UpdatePredicted()
        {
            // Bring the main state up to the latest (apply all deltas)
            IList<RailStateDelta> deltas = incomingStates.GetRange(authTick);

            RailStateDelta lastDelta = null;
            foreach (RailStateDelta delta in deltas)
            {
                // It's possible the state is null if we lost control
                // and then immediately went out of scope of the entity
                if (delta.State == null) break;
                if (delta.HasControllerData == false) break;
                StateBase.ApplyDelta(delta);
                lastDelta = delta;
            }

            if (lastDelta != null) FreeExpiredCommands(lastDelta.CommandAck);
            // TODO: Revert();

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
        public override void PreUpdate()
        {
            ApplyAuthoritativeState();
            StateBase.OverwriteFrom(AuthStateBase);
            base.PreUpdate();
        }

        public override void PostUpdate()
        {
            if (IsFrozen == false) base.PostUpdate();
        }

        public override void Removed()
        {
            RailDebug.Assert(HasStarted);

            // Set the final auth state before removing
            ApplyAuthoritativeState();
            StateBase.OverwriteFrom(AuthStateBase);

            base.Removed();
        }
        #endregion

        #region Override Functions
        /// <summary>
        ///     Called during UpdatePredicted after updating the StateBase.
        ///     Called on client controller.
        /// </summary>
        [PublicAPI]
        [Obsolete("Not very useful imo. Remove?")]
        protected virtual void Revert()
        {
        }

        /// <summary>
        ///     Populate the provided command instance.
        ///     Called on client controller.
        /// </summary>
        /// <param name="toPopulate"></param>
        [PublicAPI]
        protected virtual void WriteCommandGeneric(RailCommand toPopulate)
        {
        }

        /// <summary>
        ///     Called on every tick for frozen entities.
        ///     Called on client for all client entities.
        /// </summary>
        [PublicAPI]
        protected virtual void UpdateFrozen()
        {
        }

        /// <summary>
        ///     Update for non-controlled entities.
        ///     Called on non-controller client.
        /// </summary>
        [PublicAPI]
        protected virtual void UpdateProxy()
        {
        }

        /// <summary>
        ///     When an entity is frozen.
        ///     Called on client.
        /// </summary>
        [PublicAPI]
        protected virtual void OnFrozen()
        {
        }

        /// <summary>
        ///     When an entity is unfrozen.
        ///     Called on client.
        /// </summary>
        [PublicAPI]
        protected virtual void OnUnfrozen()
        {
        }
        #endregion
    }
}
