using System;
using System.Collections.Generic;
using System.Net;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using Xunit;

namespace Tests.Example.Tests
{
    public class ClientServerConnectionTest
    {
        [Fact]
        void ClientCanConnectAndDisconnect()
        {
            int port = Util.GetAvailablePort();

            // Start server
            Util.TestServer server = new Util.TestServer();
            server.instance.Start(Util.ip, port);
            Assert.True(Util.WaitUntil(() => server.instance.IsOnline));

            // Connect clients to server
            Util.TestClient client = new Util.TestClient();
            client.instance.Connect(new IPEndPoint(Util.ip, port));
            Assert.True(Util.WaitUntil(() => client.instance.IsConnected));
            Assert.True(Util.WaitUntil(() => server.connectedClients.Count == 1));
            Assert.NotNull(client.connection);

            // Disconnect client from server
            client.instance.Disconnect();
            Assert.True(Util.WaitUntil(() => server.connectedClients.Count == 0));
            Assert.True(Util.WaitUntil(() => !client.instance.IsConnected && client.connection == null));
            Assert.Empty(server.connectedClients);
        }

        [Theory]
        [InlineData(2, 8)]
        [InlineData(2, 64)] // Don't go too high here, we're not stress testing the network library. It will fail once the network threads can't keep up.
        void MultipleClients(int iNumberOfClientThreads, int iNumberOfClientsPerThread)
        {
            int port = Util.GetAvailablePort();

            // Start server
            Util.TestServer server = new Util.TestServer();
            server.instance.Start(Util.ip, port);
            Assert.True(Util.WaitUntil(() => server.instance.IsOnline));

            // Connect clients to server
            List<Util.TestClientsSharedThread> clients = new List<Util.TestClientsSharedThread>();
            List<Client> instances = new List<Client>();
            for (int i = 0; i < iNumberOfClientThreads; ++i)
            {
                Util.TestClientsSharedThread client = new Util.TestClientsSharedThread(iNumberOfClientsPerThread);
                foreach (var instance in client.instances)
                {
                    instance.Connect(new IPEndPoint(Util.ip, port));
                    instances.Add(instance);
                    Assert.True(Util.WaitUntil(() => instance.IsConnected));
                }
            }
            Assert.True(Util.WaitUntil(() => instances.TrueForAll(c => c.IsConnected)));
            Assert.True(Util.WaitUntil(() => server.connectedClients.Count == iNumberOfClientThreads * iNumberOfClientsPerThread));
        }
    }
}
