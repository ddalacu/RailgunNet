using System;
using System.Collections.Generic;
using System.Diagnostics;
using RailgunNet.Connection.Traffic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Scope;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RailgunNet.Connection.Server
{
    /// <summary>
    ///     A peer created by the server representing a connected client.
    /// </summary>
    public class RailServerPeer : RailPeer
    {
        public RailScope Scope { get; }

        /// <summary>
        ///     The entities controlled by this controller.
        /// </summary>
        private readonly HashSet<IServerEntity> controlledEntities = new HashSet<IServerEntity>();

        public delegate void ProcessCommandUpdateDelegate(RailServerPeer peer, RailCommandUpdate update);

        public event ProcessCommandUpdateDelegate ProcessCommandUpdate;

        public RailServerPeer(RailResource resource) : base()
        {
            Scope = new RailScope(this, resource);
        }

        public void SendPacket(
            RailPacketToClient packetToClient,
            Tick localTick,
            IEnumerable<IServerEntity> active,
            IEnumerable<IServerEntity> removed)
        {
            PrepareSend(packetToClient, localTick);

            //var tickDelta = _sincronizer.GetDelta();
            //Debug.Log("Delta "+tickDelta);

            Scope.PopulateDeltas(localTick, packetToClient, active, removed);

            foreach (var delta in packetToClient.Sent)
            {
                Scope.RegisterSent(delta.EntityId, localTick, delta.IsFrozen);
            }
        }


        public override void ProcessPacket(Tick localTick, RailPacketIncoming packetBase)
        {
            base.ProcessPacket(localTick, packetBase);

            var clientPacket = (RailPacketFromClient)packetBase;

            //Sincronizer.Add(localTick, clientPacket.SenderTick, clientPacket.TickEdit);

            Scope.IntegrateAcked(clientPacket.View);

            foreach (RailCommandUpdate update in clientPacket.CommandUpdated.Received)
            {
                ProcessCommandUpdate?.Invoke(this, update);
            }

        }


        public override bool ShouldSendEvent(RailEvent railEvent)
        {
            // Don't send an event if it's out of scope for this peer
            if (Scope.Includes(railEvent) == false)
                return false;

            return true;
        }

        public void GrantControl(IServerEntity entity)
        {
            GrantControlInternal(entity);
        }

        public void RevokeControl(IServerEntity entity)
        {
            RevokeControlInternal(entity);
        }

        public void Shutdown()
        {
            foreach (var entity in controlledEntities)
            {
                entity.AssignController(null);
            }

            controlledEntities.Clear();
        }

        /// <summary>
        ///     Adds an entity to be controlled by this peer.
        /// </summary>
        private void GrantControlInternal(IServerEntity entity)
        {
            if (entity.Controller == this) return;
            RailDebug.Assert(entity.Controller == null);

            controlledEntities.Add(entity);
            entity.AssignController(this);
        }

        /// <summary>
        ///     Remove an entity from being controlled by this peer.
        /// </summary>
        internal void RevokeControlInternal(IServerEntity entity)
        {
            RailDebug.Assert(entity.Controller == this);

            controlledEntities.Remove(entity);
            entity.AssignController(null);
        }


        public delegate void EventReceivedDelegate(RailEvent evnt, RailServerPeer sender);


        public event EventReceivedDelegate EventReceived;

        public override void HandleEventReceived(RailEvent @event)
        {
            EventReceived?.Invoke(@event, this);
        }
    }
}
