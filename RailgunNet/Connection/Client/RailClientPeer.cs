using System.Collections.Generic;
using System.Linq;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Types;

namespace RailgunNet.Connection.Client
{
    /// <summary>
    ///     The peer created by the client representing the server.
    /// </summary>
    public class RailClientPeer : RailPeer
    {
        public uint RemoteSendRate { get; }

        private readonly RailView localView;

        public RailResource Resource { get; set; }

        public RailClientPeer(
            RailResource resource,
            uint remoteSendRate) : base()
        {
            localView = new RailView();
            RemoteSendRate = remoteSendRate;
            Resource = resource;
        }

        public void SendPacket(RailPacketToServer packetToServer, Tick localTick, IEnumerable<IProduceCommands> controlledEntities)
        {
            // TODO: Sort controlledEntities by most recently sent
            PrepareSend(packetToServer, localTick);
            packetToServer.Populate(ProduceCommandUpdates(controlledEntities), localView);

            foreach (RailCommandUpdate commandUpdate in packetToServer.Sent)
            {
                commandUpdate.Entity.LastSentCommandTick = localTick;
            }
        }


        public override void ProcessPacket(Tick localTick, RailPacketIncoming packetBase)
        {
            var packetFromServer = (RailPacketFromServer)packetBase;

            base.ProcessPacket(localTick, packetBase);

            foreach (RailStateDelta delta in packetFromServer.Deltas)
            {
                localView.RecordUpdate(
                    delta.EntityId,
                    packetBase.SenderTick,
                    delta.IsFrozen);
            }
        }

        public override bool ShouldSendEvent(RailEvent railEvent) => true;


        public delegate void EventReceivedDelegate(RailEvent evnt, RailClientPeer sender);


        public event EventReceivedDelegate EventReceived;

        public override void HandleEventReceived(RailEvent @event)
        {
            EventReceived?.Invoke(@event, this);
        }

        private IEnumerable<RailCommandUpdate> ProduceCommandUpdates(IEnumerable<IProduceCommands> entities)
        {
            // If we have too many entities to fit commands for in a packet,
            // we want to round-robin sort them to avoid starvation
            return entities.OrderBy(e => e.LastSentCommandTick, Tick.DefaultComparer)
                           .Select(e => RailCommandUpdate.Create(Resource, e, e.OutgoingCommands));
        }
    }
}
