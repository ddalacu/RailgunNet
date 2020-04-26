using System.Collections.Generic;
using LiteNetLib;
using RailgunNet.Connection.Server;
using RailgunNet.Connection.Traffic;

namespace RailgunNet.Wrappers.LiteNetLib
{
    public class NetServerWrapper
    {
        private readonly RailServer server;

        private readonly Dictionary<int, NetPeerWrapper> connectedPeers = new Dictionary<int, NetPeerWrapper>();
        public NetServerWrapper(RailServer server)
        {
            this.server = server;
        }
        public void Receive(NetPeer peer,
                            NetPacketReader reader,
                            DeliveryMethod deliveryMethod)
        {
            if (connectedPeers.ContainsKey(peer.Id))
            {
                NetPeerWrapper wrapper = (NetPeerWrapper)peer.Tag;
                connectedPeers[peer.Id].Receive(peer, reader, deliveryMethod);
            }
        }
        public void Connected(NetPeer peer)
        {
            NetPeerWrapper wrapper = new NetPeerWrapper(peer);
            peer.Tag = wrapper;
            connectedPeers.Add(peer.Id, wrapper);
            this.server.AddClient(wrapper, peer.Id.ToString());
        }
        public void Disconnected(NetPeer peer, DisconnectInfo info)
        {
            if (connectedPeers.ContainsKey(peer.Id))
            {
                NetPeerWrapper wrapper = (NetPeerWrapper)peer.Tag;
                this.server.RemoveClient(wrapper);
            }
        }
    }
}
