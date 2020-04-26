using System;
using System.Net;
using LiteNetLib;
using Moq;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RailgunNet.Wrappers.LiteNetLib;

namespace Tests.Example
{
    public class ServerException : Exception
    {
        public ServerException(string msg) : base(msg)
        {
        }
    }

    public class Server
    {
        public readonly RailServer rail;

        #region Network
        private readonly EventBasedNetListener netListener;
        private readonly NetManager netManager;
        private readonly NetServerWrapper netWrapper;
        #endregion

        public Server(RailRegistry registry)
        {
            rail = new RailServer(registry);
            rail.StartRoom();
            netWrapper = new NetServerWrapper(rail);
            netListener = new EventBasedNetListener();
            netManager = new NetManager(netListener);

            netListener.ConnectionRequestEvent += (request) => request.Accept();
            netListener.NetworkReceiveEvent += (peer, reader, method) => netWrapper.Receive(peer, reader, method);
            netListener.PeerConnectedEvent += peer => netWrapper.Connected(peer);
            netListener.PeerDisconnectedEvent += (peer, info) => netWrapper.Disconnected(peer, info);
        }

        public bool IsOnline => netManager.IsRunning;

        public void Start(IPAddress address, int iPort)
        {
            if(!netManager.Start(address, IPAddress.IPv6Any, iPort))
            {
                throw new ServerException($"Could not host rail on {address}:{iPort}.");
            }
        }

        public void Update()
        {
            netManager.PollEvents();
            rail.Update();
        }

        public void Stop()
        {
            netManager.DisconnectAll();
            netManager.Stop();
        }
    }
}
