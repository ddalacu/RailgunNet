using System;
using System.Collections.Generic;
using RailgunNet.Connection.Traffic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Scope;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Types;

namespace RailgunNet.Connection.Server
{
    /// <summary>
    ///     A peer created by the server representing a connected client.
    /// </summary>
    public class RailServerPeer : RailPeer<RailPacketFromClient, RailPacketToClient>
    {
        public RailScope Scope { get; }

        public RailServerPeer(
            RailResource resource,
            IRailNetPeer netPeer,
            uint remoteSendRate,
            RailInterpreter interpreter) : base(
            resource,
            netPeer,
            remoteSendRate,
            interpreter)
        {
            Scope = new RailScope(this, resource);
        }

        /// <summary>
        ///     A connection identifier string. (TODO: Temporary)
        /// </summary>
        public string Identifier { get; set; }

        public event Action<RailServerPeer, IRailClientPacket> PacketReceived;

        public void SendPacket(
            Tick localTick,
            IEnumerable<RailEntityServer> active,
            IEnumerable<RailEntityServer> removed)
        {
            RailPacketToClient packetToClient = PrepareSend<RailPacketToClient>(localTick);
            Scope.PopulateDeltas(localTick, packetToClient, active, removed);
            base.SendPacket(packetToClient);

            foreach (RailStateDelta delta in packetToClient.Sent)
            {
                Scope.RegisterSent(delta.EntityId, localTick, delta.IsFrozen);
            }
        }

        protected override void ProcessPacket(RailPacketIncoming packetBase, Tick localTick)
        {
            base.ProcessPacket(packetBase, localTick);

            RailPacketFromClient clientPacket = (RailPacketFromClient)packetBase;
            Scope.IntegrateAcked(clientPacket.View);
            PacketReceived?.Invoke(this, clientPacket);
        }

        public override bool ShouldSendEvent(RailEvent railEvent)
        {
            // Don't send an event if it's out of scope for this peer
            if (Scope.Includes(railEvent) == false)
                return false;

            return true;
        }

        public void GrantControl(RailEntityServer entity)
        {
            GrantControlInternal(entity);
        }

        public void RevokeControl(RailEntityServer entity)
        {
            RevokeControlInternal(entity);
        }
    }
}
