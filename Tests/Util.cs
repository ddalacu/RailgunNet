using System;
using System.Collections.Generic;
using System.Threading;
using RailgunNet.Connection.Traffic;
using RailgunNet.System.Encoding.Compressors;

namespace Tests.Example
{
    public static class GameMath
    {
        public const float COORDINATE_PRECISION = 0.001f;

        public static bool CoordinatesEqual(float a, float b)
        {
            return Math.Abs(a - b) < COORDINATE_PRECISION;
        }
    }
    public static class Util
    {
        public static class Compression
        {
            public static readonly RailFloatCompressor Coordinate = new RailFloatCompressor(
                -512.0f,
                512.0f,
                GameMath.COORDINATE_PRECISION / 10.0f);
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

                Thread.Sleep(waitTimeBetweenTries);
                totalWaitTime += waitTimeBetweenTries;
                if (totalWaitTime > timeout)
                {
                    return false;
                }
            }
        }

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
    }
}
