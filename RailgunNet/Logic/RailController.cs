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
using RailgunNet.Connection.Traffic;
using RailgunNet.Factory;
using RailgunNet.Logic.Scope;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;

namespace RailgunNet.Logic
{
    public class RailController
    {
        /// <summary>
        ///     The entities controlled by this controller.
        /// </summary>
        private readonly HashSet<IRailEntity> controlledEntities;

        /// <summary>
        ///     The network I/O peer for sending/receiving data.
        /// </summary>
        protected readonly IRailNetPeer netPeer;

        public RailController(
            RailResource resource,
            IRailNetPeer netPeer = null)
        {
            controlledEntities = new HashSet<IRailEntity>();
            this.netPeer = netPeer;

#if SERVER
            Scope = new RailScope(this, resource);
#endif

            netPeer?.BindController(this);
        }

        public object UserData { get; set; }

        public virtual Tick EstimatedRemoteTick =>
            throw new InvalidOperationException(
                "Local controller has no remote tick");

        public IEnumerable<IRailEntity> ControlledEntities => controlledEntities;

        public IRailNetPeer NetPeer => netPeer;

        /// <summary>
        ///     Queues an event to send directly to this peer.
        /// </summary>
        public virtual void RaiseEvent(
            RailEvent evnt,
            ushort attempts = 3,
            bool freeWhenDone = true)
        {
            throw new InvalidOperationException(
                "Cannot raise event to local controller");
        }

        /// <summary>
        ///     Queues an event to send directly to this peer.
        /// </summary>
        public virtual void SendEvent(
            RailEvent evnt,
            ushort attempts)
        {
            throw new InvalidOperationException(
                "Cannot send event to local controller");
        }

#if SERVER
        /// <summary>
        ///     Used for setting the scope evaluator heuristics.
        /// </summary>
        public RailScopeEvaluator ScopeEvaluator
        {
            set => Scope.Evaluator = value;
        }

        /// <summary>
        ///     Used for determining which entity updates to send.
        /// </summary>
        public RailScope Scope { get; }

#endif

#if SERVER
        public void GrantControl(IRailEntity entity)
        {
            GrantControlInternal(entity);
        }

        public void RevokeControl(IRailEntity entity)
        {
            RevokeControlInternal(entity);
        }
#endif

        #region Controller

        /// <summary>
        ///     Detaches the controller from all controlled entities.
        /// </summary>
        public void Shutdown()
        {
            foreach (RailEntity entity in controlledEntities)
                entity.AssignController(null);
            controlledEntities.Clear();
        }

        /// <summary>
        ///     Adds an entity to be controlled by this peer.
        /// </summary>
        public void GrantControlInternal(IRailEntity entity)
        {
#if SERVER
            // This could happen on the client as a race condition,
            // in which case we should be able to handle it alright
            RailDebug.Assert(entity.IsRemoving == false);
#endif

            if (entity.Controller == this)
                return;
            RailDebug.Assert(entity.Controller == null);

            controlledEntities.Add(entity);
            entity.AsBase.AssignController(this);
        }

        /// <summary>
        ///     Remove an entity from being controlled by this peer.
        /// </summary>
        public void RevokeControlInternal(IRailEntity entity)
        {
            RailDebug.Assert(entity.Controller == this);

            controlledEntities.Remove(entity);
            entity.AsBase.AssignController(null);
        }

        #endregion
    }
}