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

namespace Railgun
{
    /// <summary>
    ///     Entities represent any object existent in the world. These can be
    ///     "physical" objects that move around and do things like pawns and
    ///     vehicles, or conceptual objects like scoreboards and teams that
    ///     mainly serve as blackboards for transmitting state data.
    ///     In order to register an Entity class with Railgun, tag it with the
    ///     [RegisterEntity] attribute. See RailRegistry.cs for more information.
    /// </summary>
    public abstract class RailEntity
        : IRailPoolable<RailEntity>
            , IRailEntity
    {
        #region Pooling

        IRailMemoryPool<RailEntity> IRailPoolable<RailEntity>.Pool { get; set; }

        void IRailPoolable<RailEntity>.Reset()
        {
            Reset();
        }

        #endregion

        #region Interface

        RailEntity IRailEntity.AsBase => this;

        #endregion

        #region Creation

        public static RailEntity Create(
            RailResource resource,
            int factoryType)
        {
            RailEntity entity = resource.CreateEntity(factoryType);
            entity.resource = resource;
            entity.factoryType = factoryType;
            entity.StateBase = RailState.Create(resource, factoryType);
#if CLIENT
            entity.AuthStateBase = entity.StateBase.Clone(resource);
            entity.NextStateBase = entity.StateBase.Clone(resource);
#endif
            return entity;
        }

#if SERVER
        public static T Create<T>(
            RailResource resource)
            where T : RailEntity
        {
            int factoryType = resource.GetEntityFactoryType<T>();
            return (T) Create(resource, factoryType);
        }
#endif

        #endregion

        #region Override Functions

        protected virtual void Revert()
        {
        } // Called on controller

        protected virtual void UpdateControlGeneric(RailCommand toPopulate)
        {
        } // Called on controller

        protected virtual void ApplyControlGeneric(RailCommand toApply)
        {
        } // Called on controller and server

        protected virtual void CommandMissing()
        {
        } // Called on server

        protected virtual void UpdateFrozen()
        {
        } // Called on non-controller client

        protected virtual void UpdateProxy()
        {
        } // Called on non-controller client

        protected virtual void UpdateAuth()
        {
        } // Called on server

        protected virtual void OnControllerChanged()
        {
        } // Called on all

        protected virtual void OnStart()
        {
        } // Called on all

        protected virtual void OnPostUpdate()
        {
        } // Called on all except frozen

        protected virtual void OnSunset()
        {
        } // Called on server

        protected virtual void OnShutdown()
        {
        } // Called on all

        protected abstract void OnReset();

        // Client-only
        protected virtual void OnFrozen()
        {
        }

        protected virtual void OnUnfrozen()
        {
        }

        #endregion

        // Configuration
        public virtual RailConfig.RailUpdateOrder UpdateOrder => RailConfig.RailUpdateOrder.Normal;

        public virtual bool CanFreeze => true;

        // Simulation info
        public RailRoom Room { get; set; }
        public bool HasStarted { get; private set; }
        public bool IsRemoving => RemovedTick.IsValid;
        public bool IsFrozen { get; private set; }
        public RailController Controller { get; private set; }

        protected abstract RailState StateBase { get; set; }

        // Synchronization info
        public EntityId Id { get; private set; }
        public Tick RemovedTick { get; private set; }

        /// <summary>
        ///     Whether or not this entity should be removed from a room this tick.
        /// </summary>
        public bool ShouldRemove =>
            RemovedTick.IsValid &&
            RemovedTick <= Room.Tick;

        private RailResource resource;
        private int factoryType;
        private bool deferNotifyControllerChanged;

#if SERVER
        private readonly RailDejitterBuffer<RailCommand> incomingCommands;
        private readonly RailQueueBuffer<RailStateRecord> outgoingStates;

        // The remote (client) tick of the last command we processed
        private Tick commandAck;

        // The controller at the time of entity removal
        private RailController priorController;
#endif

#if CLIENT
        public bool IsControlled { get { return (this.Controller != null); } }

        /// <summary>
        /// The tick of the last authoritative state.
        /// </summary>
        public Tick AuthTick { get { return this.authTick; } }

        /// <summary>
        /// The tick of the next authoritative state. May be invalid.
        /// </summary>
        public Tick NextTick { get { return this.nextTick; } }

        /// <summary>
        /// Returns the number of ticks ahead we are, for extrapolation.
        /// Note that this does not take client-side prediction into account.
        /// </summary>
        public int TicksAhead { get { return this.Room.Tick - this.authTick; } }

        protected abstract RailState AuthStateBase { get; set; }
        protected abstract RailState NextStateBase { get; set; }
        public IEnumerable<RailCommand> OutgoingCommands { get { return this.outgoingCommands; } }
        public Tick LastSentCommandTick { get; set; } // The last local tick we sent our commands to the server

        private readonly RailDejitterBuffer<RailStateDelta> incomingStates;
        private readonly Queue<RailCommand> outgoingCommands;

        private Tick authTick;
        private Tick nextTick;
        private bool shouldBeFrozen;
#endif

        protected RailEntity()
        {
#if SERVER
            // We use no divisor for storing commands because commands are sent in
            // batches that we can use to fill in the holes between send ticks
            incomingCommands =
                new RailDejitterBuffer<RailCommand>(
                    RailConfig.DEJITTER_BUFFER_LENGTH);
            outgoingStates =
                new RailQueueBuffer<RailStateRecord>(
                    RailConfig.DEJITTER_BUFFER_LENGTH);
#endif

#if CLIENT
            this.incomingStates =
              new RailDejitterBuffer<RailStateDelta>(
                RailConfig.DEJITTER_BUFFER_LENGTH,
                RailConfig.SERVER_SEND_RATE);
            this.outgoingCommands =
              new Queue<RailCommand>();
#endif

            Reset();
        }

        private void Reset()
        {
            // TODO: Is this complete/usable?

            Room = null;
            HasStarted = false;
            IsFrozen = false;
            resource = null;

            Id = EntityId.INVALID;
            Controller = null;

            // We always notify a controller change at start
            deferNotifyControllerChanged = true;

#if SERVER
            outgoingStates.Clear();
            incomingCommands.Clear();
#endif

#if CLIENT
            this.LastSentCommandTick = Tick.START;
            this.IsFrozen = true; // Entities start frozen on client
            this.shouldBeFrozen = true;

            this.incomingStates.Clear();
            RailPool.DrainQueue(this.outgoingCommands);

            this.authTick = Tick.START;
            this.nextTick = Tick.INVALID;
#endif

            ResetStates();
            OnReset();
        }

        private void ResetStates()
        {
            if (StateBase != null)
                RailPool.Free(StateBase);
#if CLIENT
            if (this.AuthStateBase != null)
                RailPool.Free(this.AuthStateBase);
            if (this.NextStateBase != null)
                RailPool.Free(this.NextStateBase);
#endif

            StateBase = null;
#if CLIENT
            this.AuthStateBase = null;
            this.NextStateBase = null;
#endif
        }

        public void AssignId(EntityId id)
        {
            Id = id;
        }

        public void AssignController(RailController controller)
        {
            if (Controller != controller)
            {
                Controller = controller;
                ClearCommands();
                deferNotifyControllerChanged = true;
            }
        }

        #region Lifecycle and Loop

        public void Startup()
        {
#if CLIENT
            this.UpdateAuthState();
            this.StateBase.OverwriteFrom(this.AuthStateBase);
#endif

            if (HasStarted == false)
                OnStart();
            HasStarted = true;
            NotifyControllerChanged();
        }

#if SERVER
        public void ServerUpdate()
        {
            UpdateAuth();

            RailCommand latest = GetLatestCommand();
            if (latest != null)
            {
                ApplyControlGeneric(latest);
                latest.IsNewCommand = false;

                // Use the remote tick rather than the last applied tick
                // because we might be skipping some commands to keep up
                UpdateCommandAck(Controller.EstimatedRemoteTick);
            }
            else if (Controller != null)
            {
                // We have no command to work from but might still want to
                // do an update in the command sequence (if we have a controller)
                CommandMissing();
            }
        }
#endif

#if CLIENT
        public void ClientUpdate(Tick localTick)
        {
            this.SetFreeze(this.shouldBeFrozen);
            if (this.IsFrozen)
            {
                this.UpdateFrozen();
            }
            else
            {
                if (this.Controller == null)
                {
                    this.UpdateProxy();
                }
                else
                {
                    this.nextTick = Tick.INVALID;
                    this.UpdateControlled(localTick);
                    this.UpdatePredicted();
                }
            }
        }
#endif

        public void PostUpdate()
        {
#if CLIENT
            if (this.IsFrozen == false)
#endif
            {
                OnPostUpdate();
            }
        }

#if SERVER
        public void MarkForRemoval()
        {
            // We'll remove on the next tick since we're probably 
            // already mid-way through evaluating this tick
            RemovedTick = Room.Tick + 1;

            // Notify our inheritors that we are being removed next tick
            OnSunset();
        }
#endif

        public void Shutdown()
        {
            RailDebug.Assert(HasStarted);

#if SERVER
            // Automatically revoke control but keep a history for 
            // sending the final controller data to the client.
            if (Controller != null)
            {
                priorController = Controller;
                Controller.RevokeControlInternal(this);
                NotifyControllerChanged();
            }
#endif

#if CLIENT
            // Set the final auth state before removing
            this.UpdateAuthState();
            this.StateBase.OverwriteFrom(this.AuthStateBase);
#endif

            OnShutdown();
        }

        #endregion

        private void ClearCommands()
        {
#if SERVER
            incomingCommands.Clear();
            commandAck = Tick.INVALID;
#endif

#if CLIENT
            this.outgoingCommands.Clear();
            this.LastSentCommandTick = Tick.START;
#endif
        }

#if SERVER
        public void StoreRecord()
        {
            RailStateRecord record =
                RailState.CreateRecord(
                    resource,
                    Room.Tick,
                    StateBase,
                    outgoingStates.Latest);
            if (record != null)
                outgoingStates.Store(record);
        }

        public RailStateDelta ProduceDelta(
            Tick basisTick,
            RailController destination,
            bool forceAllMutable)
        {
            // Flags for special data modes
            bool includeControllerData =
                destination == Controller ||
                destination == priorController;
            bool includeImmutableData = basisTick.IsValid == false;

            return RailState.CreateDelta(
                resource,
                Id,
                StateBase,
                outgoingStates.LatestFrom(basisTick),
                includeControllerData,
                includeImmutableData,
                commandAck,
                RemovedTick,
                forceAllMutable);
        }

        public void ReceiveCommand(RailCommand command)
        {
            if (incomingCommands.Store(command))
                command.IsNewCommand = true;
            else
                RailPool.Free(command);
        }

        private RailCommand GetLatestCommand()
        {
            if (Controller != null)
                return
                    incomingCommands.GetLatestAt(
                        Controller.EstimatedRemoteTick);
            return null;
        }

        private void UpdateCommandAck(Tick latestCommandTick)
        {
            bool shouldAck =
                commandAck.IsValid == false ||
                latestCommandTick > commandAck;
            if (shouldAck)
                commandAck = latestCommandTick;
        }
#endif

#if CLIENT
        public float ComputeInterpolation(
          float tickDeltaTime,
          float timeSinceTick)
        {
            if (this.nextTick == Tick.INVALID)
                throw new InvalidOperationException("Next state is invalid");

            float curTime = this.authTick.ToTime(tickDeltaTime);
            float nextTime = this.nextTick.ToTime(tickDeltaTime);
            float showTime = this.Room.Tick.ToTime(tickDeltaTime) + timeSinceTick;

            float progress = showTime - curTime;
            float span = nextTime - curTime;
            if (span <= 0.0f)
                return 0.0f;
            return progress / span;
        }

        public bool HasReadyState(Tick tick)
        {
            return (this.incomingStates.GetLatestAt(tick) != null);
        }

        /// <summary>
        /// Applies the initial creation delta.
        /// </summary>
        public void PrimeState(RailStateDelta delta)
        {
            RailDebug.Assert(delta.IsFrozen == false);
            RailDebug.Assert(delta.IsRemoving == false);
            RailDebug.Assert(delta.HasImmutableData);
            this.AuthStateBase.ApplyDelta(delta);
        }

        /// <summary>
        /// Returns true iff we stored the delta. False if it will leak.
        /// </summary>
        public bool ReceiveDelta(RailStateDelta delta)
        {
            bool stored = false;
            if (delta.IsFrozen)
            {
                // Frozen deltas have no state data, so we need to treat them
                // separately when doing checks based on state content
                stored = this.incomingStates.Store(delta);
            }
            else
            {
                if (delta.IsRemoving)
                    this.RemovedTick = delta.RemovedTick;
                stored = this.incomingStates.Store(delta);
            }

            return stored;
        }

        private void CleanCommands(Tick ackTick)
        {
            if (ackTick.IsValid == false)
                return;

            while (this.outgoingCommands.Count > 0)
            {
                RailCommand command = this.outgoingCommands.Peek();
                if (command.ClientTick > ackTick)
                    break;
                RailPool.Free(this.outgoingCommands.Dequeue());
            }
        }

        private void UpdateControlled(Tick localTick)
        {
            RailDebug.Assert(this.Controller != null);
            if (this.outgoingCommands.Count < RailConfig.COMMAND_BUFFER_COUNT)
            {
                RailCommand command = RailCommand.Create(this.resource);

                command.ClientTick = localTick;
                command.IsNewCommand = true;

                this.UpdateControlGeneric(command);
                this.outgoingCommands.Enqueue(command);
            }
        }

        private void UpdateAuthState()
        {
            // Apply all un-applied deltas to the auth state
            IEnumerable<RailStateDelta> toApply =
              this.incomingStates.GetRangeAndNext(
                this.authTick,
                this.Room.Tick,
                out RailStateDelta next);

            RailStateDelta lastDelta = null;
            foreach (RailStateDelta delta in toApply)
            {
                if (delta.IsFrozen == false)
                    this.AuthStateBase.ApplyDelta(delta);
                this.shouldBeFrozen = delta.IsFrozen;
                this.authTick = delta.Tick;
                lastDelta = delta;
            }

            if (lastDelta != null)
            {
                // Update the control status based on the most recent delta
                this.Room.RequestControlUpdate(this, lastDelta);
            }

            // If there was a next state, update the next state
            bool canGetNext = (this.shouldBeFrozen == false);
            if (canGetNext && (next != null) && (next.IsFrozen == false))
            {
                this.NextStateBase.OverwriteFrom(this.AuthStateBase);
                this.NextStateBase.ApplyDelta(next);
                this.nextTick = next.Tick;
            }
            else
            {
                this.nextTick = Tick.INVALID;
            }
        }

        private void UpdatePredicted()
        {
            // Bring the main state up to the latest (apply all deltas)
            IList<RailStateDelta> deltas =
              this.incomingStates.GetRange(this.authTick);

            RailStateDelta lastDelta = null;
            foreach (var delta in deltas)
            {
                // It's possible the state is null if we lost control
                // and then immediately went out of scope of the entity
                if (delta.State == null)
                    break;
                if (delta.HasControllerData == false)
                    break;
                this.StateBase.ApplyDelta(delta);
                lastDelta = delta;
            }

            if (lastDelta != null)
                this.CleanCommands(lastDelta.CommandAck);
            this.Revert();

            // Forward-simulate
            foreach (RailCommand command in this.outgoingCommands)
            {
                this.ApplyControlGeneric(command);
                command.IsNewCommand = false;
            }
        }

        private void SetFreeze(bool isFrozen)
        {
            if ((this.IsFrozen == false) && isFrozen)
                this.OnFrozen();
            else if (this.IsFrozen && (isFrozen == false))
                this.OnUnfrozen();
            this.IsFrozen = isFrozen;
        }
#endif

        private void NotifyControllerChanged()
        {
            if (deferNotifyControllerChanged)
                OnControllerChanged();
            deferNotifyControllerChanged = false;
        }
    }

    /// <summary>
    ///     Handy shortcut class for auto-casting the state.
    /// </summary>
    public abstract class RailEntity<TState>
        : RailEntity
            , IRailEntity<TState>
        where TState : RailState, new()
    {
        protected override RailState StateBase
        {
            get => State;
            set => State = (TState) value;
        }

#if CLIENT
        protected override RailState AuthStateBase
        {
            get { return this.authState; }
            set { this.authState = (TState)value; }
        }

        protected override RailState NextStateBase
        {
            get { return this.nextState; }
            set { this.nextState = (TState)value; }
        }
#endif

        public TState State { get; private set; }

#if CLIENT
        private TState authState;
        private TState nextState;

        /// <summary>
        /// Returns the current dejittered authoritative state from the server.
        /// Will return null if the entity is locally controlled (use State).
        /// </summary>
        public TState AuthState
        {
            get
            {
                // Not valid if we're controlling
                if (this.IsControlled)
                    return null;
                return this.authState;
            }
        }

        /// <summary>
        /// Returns the next dejittered authoritative state from the server. Will 
        /// return null none is available or if the entity is locally controlled.
        /// </summary>
        public TState NextState
        {
            get
            {
                // Not valid if we're controlling
                if (this.IsControlled)
                    return null;
                // Only return if we have a valid next state assigned
                if (this.NextTick.IsValid)
                    return this.nextState;
                return null;
            }
        }
#endif
    }

    /// <summary>
    ///     Handy shortcut class for auto-casting the state and command.
    /// </summary>
    public abstract class RailEntity<TState, TCommand>
        : RailEntity<TState>
            , IRailEntity<TState>
        where TState : RailState, new()
        where TCommand : RailCommand
    {
        protected override void UpdateControlGeneric(RailCommand toPopulate)
        {
            UpdateControl((TCommand) toPopulate);
        }

        protected override void ApplyControlGeneric(RailCommand toApply)
        {
            ApplyControl((TCommand) toApply);
        }

        protected virtual void UpdateControl(TCommand toPopulate)
        {
        }

        protected virtual void ApplyControl(TCommand toApply)
        {
        }
    }
}