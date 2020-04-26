using System;
using LiteNetLib;
using RailgunNet.Connection.Traffic;
using RailgunNet.Logic;
using System.Net;
using System.Net.Sockets;

namespace RailgunNet.Wrappers.LiteNetLib
{
    public class NetPeerWrapper : IRailNetPeer
    {
        public readonly NetPeer peer;
        public NetPeerWrapper(NetPeer peer)
        {
            this.peer = peer;
        }
        public void Receive(NetPeer peer,
                            NetPacketReader reader,
                            DeliveryMethod deliveryMethod)
        {
            byte[] data = reader.GetRemainingBytes();
            PayloadReceived?.Invoke(this, data, data.Length);
        }

        #region IRailNetPeer
        public object PlayerData { get; set; }

        public float? Ping => peer.Ping;

        public event RailNetPeerEvent PayloadReceived;

        public void SendPayload(byte[] buffer, int length)
        {
            peer.Send(buffer, 0, length, DeliveryMethod.ReliableUnordered);
        }
        #endregion
    }
}
