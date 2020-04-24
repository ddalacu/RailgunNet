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

using RailgunNet.Factory;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic
{
    /// <summary>
    ///     Entities represent any object existent in the world. These can be
    ///     "physical" objects that move around and do things like pawns and
    ///     vehicles, or conceptual objects like scoreboards and teams that
    ///     mainly serve as blackboards for transmitting state data.
    /// </summary>
    public abstract class RailEntity : IRailPoolable<RailEntity>, IRailEntity
    {
        private bool deferNotifyControllerChanged;
        private int factoryType;

        protected IRailCommandConstruction CommandCreator { get; private set; }

        // Configuration
        public virtual RailConfig.RailUpdateOrder UpdateOrder => RailConfig.RailUpdateOrder.Normal;

        protected abstract RailState StateBase { get; set; }
        public Tick RemovedTick { get; protected set; }

        /// <summary>
        ///     Whether or not this entity should be removed from a room this tick.
        /// </summary>
        public bool ShouldRemove => RemovedTick.IsValid && RemovedTick <= Room.Tick;

        public bool CanFreeze => true;

        #region Interface
        RailEntity IRailEntity.AsBase => this;
        #endregion

        // Simulation info
        public RailRoom Room { get; set; }
        public bool HasStarted { get; private set; }
        public bool IsRemoving => RemovedTick.IsValid;
        public bool IsFrozen { get; protected set; }
        public RailController Controller { get; private set; }

        // Synchronization info
        public EntityId Id { get; private set; }

        protected virtual void Reset()
        {
            // TODO: Is this complete/usable?

            Room = null;
            HasStarted = false;
            IsFrozen = false;
            CommandCreator = null;

            Id = EntityId.INVALID;
            Controller = null;

            // We always notify a controller change at start
            deferNotifyControllerChanged = true;
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

        protected virtual void ClearCommands()
        {
        }

        protected void NotifyControllerChanged()
        {
            if (deferNotifyControllerChanged) OnControllerChanged();
            deferNotifyControllerChanged = false;
        }

        #region Pooling
        IRailMemoryPool<RailEntity> IRailPoolable<RailEntity>.Pool { get; set; }

        void IRailPoolable<RailEntity>.Reset()
        {
            Reset();
        }
        #endregion

        #region Creation
        protected virtual void InitState(IRailStateConstruction creator, RailState initialState)
        {
            StateBase = initialState;
        }

        public static RailEntity Create(RailResource resource, int factoryType)
        {
            RailEntity entity = resource.CreateEntity(factoryType);
            entity.CommandCreator = resource;
            entity.factoryType = factoryType;
            entity.InitState(resource, RailState.Create(resource, factoryType));
            return entity;
        }
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

        protected virtual void OnPreUpdate()
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

        #region Lifecycle and Loop
        public virtual void PreUpdate()
        {
            if (HasStarted == false) OnPreUpdate();
            HasStarted = true;
            NotifyControllerChanged();
        }

        public virtual void PostUpdate()
        {
            OnPostUpdate();
        }

        public virtual void Shutdown()
        {
            RailDebug.Assert(HasStarted);
            OnShutdown();
        }
        #endregion
    }

#if false
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

        public TState State { get; private set; }

#if CLIENT
        protected override RailState AuthStateBase
        {
            get => authState;
            set => authState = (TState) value;
        }

        protected override RailState NextStateBase
        {
            get => nextState;
            set => nextState = (TState) value;
        }
#endif

#if CLIENT
        private TState authState;
        private TState nextState;

        /// <summary>
        ///     Returns the current dejittered authoritative state from the server.
        ///     Will return null if the entity is locally controlled (use State).
        /// </summary>
        public TState AuthState
        {
            get
            {
                // Not valid if we're controlling
                if (IsControlled)
                    return null;
                return authState;
            }
        }

        /// <summary>
        ///     Returns the next dejittered authoritative state from the server. Will
        ///     return null none is available or if the entity is locally controlled.
        /// </summary>
        public TState NextState
        {
            get
            {
                // Not valid if we're controlling
                if (IsControlled)
                    return null;
                // Only return if we have a valid next state assigned
                if (NextTick.IsValid)
                    return nextState;
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
#endif
}
