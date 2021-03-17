using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using RailgunNet.Connection.Traffic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using RailgunNet.Util.Pooling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RailgunNet.Connection.Client
{
    [PublicAPI]
    public class RailClient : RailConnection
    {
        private readonly uint _remoteSendRate;
        private readonly uint _sendRate;

        /// <summary>
        ///     The local simulation tick, used for commands
        /// </summary>
        public Tick LocalTick { get; set; }

        /// <summary>
        ///     The peer for our connection to the server.
        /// </summary>
        [PublicAPI] [CanBeNull] public RailClientPeer ServerPeer { get; private set; }

        public RailClient(RailRegistry<IClientEntity> registry, uint remoteSendRate, uint sendRate) : base(registry)
        {
            this._remoteSendRate = remoteSendRate;
            this._sendRate = sendRate;
            ServerPeer = null;
            LocalTick = Tick.INVALID;
            Room = null;
        }

        /// <summary>
        ///     The client's room instance. TODO: Multiple rooms?
        /// </summary>
        [CanBeNull]
        private RailClientRoom Room { get; set; }

        public uint SendRate => _sendRate;

        public uint RemoteSendRate => _remoteSendRate;

        public RailClientRoom StartRoom()
        {
            Room = new RailClientRoom(Resource, this);
            return Room;
        }

        public void SetDisconnected()
        {
            ServerPeer = null;
        }

        public void SetConnected()
        {
            LocalTick = Tick.INVALID;
            RailDebug.Assert(ServerPeer == null, "Overwriting peer");
            ServerPeer = new RailClientPeer(Resource, RemoteSendRate);
            ServerPeer.EventReceived += OnEventReceived;
            Connected?.Invoke(ServerPeer);

            Debug.Log("Connected");
        }

        protected void OnEventReceived(RailEvent evnt, RailClientPeer sender)
        {
            Room.HandleEvent(evnt);
        }

        [PublicAPI] public event Action<RailClientPeer> Connected;
        [PublicAPI] public event Action<RailClientPeer> Disconnected;

        private RailInterpreter _interpreter = new RailInterpreter();

        private RailPacketFromServer _packetFromServer = new RailPacketFromServer();

        private RailPacketToServer _packetToServer = new RailPacketToServer();


        public bool Update(out ArraySegment<byte> toSend)
        {
            if (ServerPeer == null)
                return false;

            if (LocalTick == Tick.INVALID) //wait first packet
                return false;

            LocalTick = LocalTick.GetNext();

            DoStart();

            Room.ClientUpdate(LocalTick);

            //Debug.Log("Average "+ _average.Average);

            if (LocalTick.IsSendTick(SendRate))
            {
                _packetToServer.Reset();
                ServerPeer.SendPacket(_packetToServer, LocalTick, Room.ControlledEntities.OfType<IProduceCommands>());

                toSend = Interpreter.SendPacket(Resource, _packetToServer);

                _syncHelper.RegisterSentPacket(LocalTick);
                return true;
            }

            return false;
        }


        public class TimeSyncHelper
        {
            private DoubleMovingAverage _average = new DoubleMovingAverage(20);

            private long[] _timeStamps = new long[256];

            public double RTTMilliseconds => _average.Average;

            private static double _frequencyMilliseconds = Stopwatch.Frequency / 1000d;

            public TimeSyncHelper(int samples)
            {
                _average = new DoubleMovingAverage(samples);
            }

            public void RegisterSentPacket(Tick tick)
            {
                if (tick.IsValid == false)
                    return;

                var index = tick.RawValue % _timeStamps.Length;
                _timeStamps[index] = Stopwatch.GetTimestamp();
            }

            public void AckTick(Tick tick, TimeSpan processingSpan)
            {
                if (tick.IsValid == false)
                    return;

                var index = tick.RawValue % _timeStamps.Length;

                var timeStamp = _timeStamps[index];

                if (timeStamp != 0)
                {
                    var localDelta = Stopwatch.GetTimestamp() - timeStamp;

                    var deltaMs = localDelta / _frequencyMilliseconds;

                    var wireDuration = deltaMs - processingSpan.TotalMilliseconds;

                    _average.ComputeAverage(wireDuration);
                }
            }

        }

        private IntMovingAverage _deltaMovingAverage = new IntMovingAverage(20);

        public TimeSyncHelper _syncHelper = new TimeSyncHelper(20);

        public double Bonus;

        public double RTTMilliseconds => _syncHelper.RTTMilliseconds;

        public void ProcessData(ArraySegment<byte> segment)
        {
            try
            {
                var bitBuffer = _interpreter.LoadData(segment);

                _packetFromServer.Reset();
                _packetFromServer.Decode(Resource, bitBuffer);

                if (bitBuffer.IsFinished)
                {
                    if (LocalTick == Tick.INVALID)
                        LocalTick = _packetFromServer.SenderTick;

                    var processDelay = TimeSpan.FromMilliseconds(_packetFromServer.PingProcessDelay);

                    _syncHelper.AckTick(_packetFromServer.LastAckTick, processDelay);

                    ServerPeer.ProcessPacket(LocalTick, _packetFromServer);

                    //var ping = ServerPeer.PingMilliseconds;
                    //var wireDuration = ping / 2f;
                    //var add = (int)Math.Ceiling(wireDuration);

                    uint buffer = 3;

                    var delta = (LocalTick + 1) - buffer - (_packetFromServer.SenderTick);//add one to our tick because the server sends it's tick at the end of tick,
                                                                                          //and after processing all our packets we simulate and at begining of simulate we increment
                    _deltaMovingAverage.ComputeAverage(delta);


                    var tickMs = 1000 / 64d;

                    var wireTicks = (_syncHelper.RTTMilliseconds / 2d) / (tickMs);

                    //Debug.Log(_syncHelper.RTTMilliseconds / 2d);

                    if (_deltaMovingAverage.IsFull)
                    {
                        var average = -(_deltaMovingAverage.Average - wireTicks * 2);

                        //Debug.Log(_deltaMovingAverage.Average);

                        var intAverage = (int)Math.Round(average);

                        if (Math.Abs(average) > 5)
                        {
                            LocalTick += intAverage;
                            _deltaMovingAverage.Clear();
                            Debug.LogError($"Client updating, delta: {average} {intAverage}");

                            Bonus = 0;
                        }
                        else
                        {
                            Bonus = average;
                        }
                    }


                    OnPacketReceived(_packetFromServer);
                }
                else
                {
                    RailDebug.LogError("Bad packet read, discarding...");
                }
            }
            catch (Exception e)
            {
                RailDebug.LogError("Error during packet read: " + e);
            }
        }

        private void OnPacketReceived(RailPacketFromServer packet)
        {
            if (Room == null)
            {
                foreach (RailStateDelta delta in packet.Deltas)
                {
                    RailPool.Free(delta);
                }
            }
            else
            {
                foreach (RailStateDelta delta in packet.Deltas)
                {
                    if (Room.ProcessDelta(delta) == false)
                    {
                        RailPool.Free(delta);
                    }
                }
            }

            foreach (RailEvent @event in packet.Events)
            {
                RailPool.Free(@event);
            }
        }
    }
}
