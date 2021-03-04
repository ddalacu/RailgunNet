using System;
using System.Collections.Generic;
using System.Linq;
using RailgunNet.Connection.Traffic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Types;
using RailgunNet.Util;

namespace RailgunNet.Connection.Client
{
    /// <summary>
    ///     The peer created by the client representing the server.
    /// </summary>
    public class RailClientPeer : RailPeer<RailPacketFromServer, RailPacketToServer>
    {
        public uint RemoteSendRate { get; }

        private readonly RailView localView;

        public RailClientPeer(
            RailResource resource,
            IRailNetPeer netPeer,
            uint remoteSendRate,
            RailInterpreter interpreter) : base(
            resource,
            netPeer,
            remoteSendRate,
            interpreter)
        {
            localView = new RailView();
            RemoteSendRate = remoteSendRate;
        }

        public event Action<RailPacketFromServer> PacketReceived;

        public void SendPacket(Tick localTick, IEnumerable<RailEntityBase> controlledEntities)
        {
            // TODO: Sort controlledEntities by most recently sent

            RailPacketToServer packet = PrepareSend<RailPacketToServer>(localTick);
            packet.Populate(ProduceCommandUpdates(controlledEntities), localView);

            // Send the packet
            base.SendPacket(packet);

            foreach (RailCommandUpdate commandUpdate in packet.Sent)
            {
                commandUpdate.Entity.LastSentCommandTick = localTick;
            }
        }

        protected override void ProcessPacket(RailPacketIncoming packetBase, Tick localTick)
        {
            base.ProcessPacket(packetBase, localTick);

            RailPacketFromServer packetFromServer = (RailPacketFromServer)packetBase;
            foreach (RailStateDelta delta in packetFromServer.Deltas)
            {
                localView.RecordUpdate(
                    delta.EntityId,
                    packetBase.SenderTick,
                    localTick,
                    delta.IsFrozen);
            }

            PacketReceived?.Invoke(packetFromServer);
        }

        public override bool ShouldSendEvent(RailEvent railEvent) => true;

        private IEnumerable<RailCommandUpdate> ProduceCommandUpdates(
            IEnumerable<RailEntityBase> entities)
        {
            // If we have too many entities to fit commands for in a packet,
            // we want to round-robin sort them to avoid starvation
            return entities.Select(e => e as RailEntityClient)
                           .OrderBy(e => e.LastSentCommandTick, Tick.CreateComparer())
                           .Select(e => RailCommandUpdate.Create(Resource, e, e.OutgoingCommands));
        }
    }
}
