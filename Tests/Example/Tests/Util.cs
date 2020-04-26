using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;

namespace Tests.Example.Tests
{
    public static class Util
    {
        public static IPAddress ip = IPAddress.Loopback;

        private static object UsedPortsLock = new object();
        private static HashSet<int> UsedPorts = new HashSet<int>();
        public static int GetAvailablePort()
        {
            lock (UsedPortsLock)
            {
                int iPort = FindAvailablePort(3000);
                UsedPorts.Add(iPort);
                if (iPort == 0)
                {
                    throw new Exception("Could not find any available ports.");
                }
                return iPort;
            }
        }
        public static bool WaitUntil(Func<bool> condition)
        {
            return WaitUntil(condition, TimeSpan.FromMilliseconds(500));
        }

        public static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            TimeSpan totalWaitTime = TimeSpan.Zero;
            TimeSpan waitTimeBetweenTries = TimeSpan.FromMilliseconds(10);
            while (true)
            {
                if (condition())
                {
                    return true;
                }
                else
                {
                    Thread.Sleep(waitTimeBetweenTries);
                    totalWaitTime += waitTimeBetweenTries;
                    if (totalWaitTime > timeout)
                    {
                        return false;
                    }
                }
            }
        }
        public class TestServer
        {
            public readonly Server instance = new Server(Registry.Get(Component.Server));
            public readonly UpdateThread loop;
            public readonly List<RailServerPeer> connectedClients = new List<RailServerPeer>();

            public TestServer()
            {
                loop = new UpdateThread(instance.Update);
                instance.rail.ClientAdded += peer =>
                {
                    connectedClients.Add(peer);
                };
                instance.rail.ClientRemoved += peer =>
                {
                    connectedClients.Remove(peer);
                };
            }
        }
        public class TestClient
        {
            public readonly Client instance = new Client(Registry.Get(Component.Client));
            public readonly UpdateThread loop;
            public RailClientPeer connection = null;

            public TestClient()
            {
                loop = new UpdateThread(instance.Update);
                instance.rail.Connected += peer =>
                {
                    connection = peer;
                };
                instance.rail.Disconnected += peer =>
                {
                    if (connection != peer)
                    {
                        throw new ArgumentException(nameof(peer));
                    }
                    connection = null;
                };
            }
        }
        public class TestClientsSharedThread
        {
            public readonly List<Client> instances = new List<Client>();
            public readonly UpdateThread loop;

            public TestClientsSharedThread(int iNumberOfClients)
            {
                for (int i = 0; i < iNumberOfClients; ++i)
                {
                    instances.Add(new Client(Registry.Get(Component.Client)));
                }
                loop = new UpdateThread(this.Update);
            }

            private void Update()
            {
                foreach (var client in instances)
                {
                    client.Update();
                }
            }
        }
        private static int FindAvailablePort(int startingPort)
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            var tcpConnectionPorts = properties.GetActiveTcpConnections()
                                               .Where(n => n.LocalEndPoint.Port >= startingPort)
                                               .Select(n => n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            var tcpListenerPorts = properties.GetActiveTcpListeners()
                                             .Where(n => n.Port >= startingPort)
                                             .Select(n => n.Port);

            //getting active udp listeners
            var udpListenerPorts = properties.GetActiveUdpListeners()
                                             .Where(n => n.Port >= startingPort)
                                             .Select(n => n.Port);

            var port = Enumerable.Range(startingPort, ushort.MaxValue)
                                 .Where(i => !UsedPorts.Contains(i))
                                 .Where(i => !tcpConnectionPorts.Contains(i))
                                 .Where(i => !tcpListenerPorts.Contains(i))
                                 .Where(i => !udpListenerPorts.Contains(i))
                                 .FirstOrDefault();

            return port;
        }
    }
}
