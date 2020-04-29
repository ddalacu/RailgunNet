using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Connection.Traffic;

namespace Tests.Example.Tests
{
    public static class Util
    {
        public class InMemoryNetPeerWrapper : IRailNetPeer
        {
            private readonly List<byte[]> receivedBuffer = new List<byte[]>();
            public event Action<ArraySegment<byte>> OnSendPayload;
            public virtual void ReceivePayload(ArraySegment<byte> buffer)
            {
                receivedBuffer.Add(buffer.ToArray());
            }

            public void PollEvents()
            {
                receivedBuffer.ForEach(buffer => PayloadReceived?.Invoke(this, buffer));
                receivedBuffer.Clear();
            }

            #region IRailNetPeer
            public object PlayerData { get; set; }

            public float? Ping => 0;

            public event RailNetPeerEvent PayloadReceived;

            public virtual void SendPayload(ArraySegment<byte> buffer)
            {
                OnSendPayload?.Invoke(buffer);
            }
            #endregion
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
    }
}
