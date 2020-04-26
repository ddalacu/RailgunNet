using System.Net;
using LiteNetLib;
using RailgunNet.Connection.Client;
using RailgunNet.Factory;
using RailgunNet.Wrappers.LiteNetLib;

namespace Tests.Example
{
    public class Client
    {
        public readonly RailClient rail;

        #region Network
        private readonly EventBasedNetListener netListener;
        private readonly NetManager netManager;
        private NetPeerWrapper netWrapper;
        #endregion

        public Client(RailRegistry registry)
        {
            rail = new RailClient(registry);
            rail.StartRoom();
            netListener = new EventBasedNetListener();
            netManager = new NetManager(netListener)
            {
                MaxConnectAttempts = 5
            };

            netListener.PeerConnectedEvent += peer =>
            {
                netWrapper = new NetPeerWrapper(peer);
                rail.SetPeer(netWrapper);
            };
            netListener.PeerDisconnectedEvent += (peer, info) =>
            {
                rail.SetPeer(null);
                netWrapper = null;
            };
            netListener.NetworkReceiveEvent += (peer, reader, method) =>
                netWrapper?.Receive(peer, reader, method);
        }

        public bool IsConnected => netWrapper != null;

        public void Connect(IPEndPoint endPoint)
        {
            if (netManager.Start())
            {
                NetPeer peer = netManager.Connect(endPoint, "");
            }
        }
        public void Update()
        {
            netManager.PollEvents();
            rail.Update();
        }

        public void Disconnect()
        {
            if (netWrapper != null)
            {
                netManager.DisconnectPeer(netWrapper.peer);
                netManager.Flush();
                netManager.Stop();
            }
        }
    }
}
