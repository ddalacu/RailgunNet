using LiteNetLib;

namespace RailgunNet.Wrappers.LiteNetLib
{
    public static class Common
    {
        public delegate void NetworkReceiveEvent(NetPeer peer,
                                                 NetPacketReader reader,
                                                 DeliveryMethod deliveryMethod);
        public delegate void PeerConnectedEvent(NetPeer peer);
        public delegate void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo info);
    }
}
