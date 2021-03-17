using System;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using RailgunNet.Connection.Traffic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using RailgunNet.Util.Pooling;
using Debug = UnityEngine.Debug;

namespace RailgunNet.Connection.Server
{
    public class TickSincronizer
    {
        private IntMovingAverage _deltaAverage = new IntMovingAverage(32);

        private Tick _latestReceivedTick;

        private byte _lastTickEdit;

        public byte LastTickEdit => _lastTickEdit;

        public double GetDelta()
        {
            if (_deltaAverage.IsFull)
            {
                return _deltaAverage.Average + 3;
            }

            return 0;
        }

        public void Add(Tick targetTick, Tick tick, byte tickEdit)
        {

            if (tickEdit != _lastTickEdit)
            {
                _deltaAverage.Clear();
                Debug.Log("Client updated tick!");
            }

            _lastTickEdit = tickEdit;

            if (_latestReceivedTick.IsValid == false)
                _latestReceivedTick = tick;
            else
            if (tick > _latestReceivedTick)
                _latestReceivedTick = tick;

            var delta = targetTick - _latestReceivedTick;
            _deltaAverage.ComputeAverage(delta);
        }
    }

    public delegate void ServerSendDataDelegate(RailServerPeer railServerPeer, ArraySegment<byte> segment);

    /// <summary>
    ///     Server is the core executing class on the server. It is responsible for
    ///     managing connection contexts and payload I/O.
    /// </summary>
    [PublicAPI]
    public class RailServer : RailConnection
    {
        private readonly ServerSendDataDelegate _serverSendData;

        private readonly uint remoteSendRate;
        private readonly uint sendRate;

        public RailServer(ServerSendDataDelegate serverSendData, RailRegistry<IServerEntity> registry, uint remoteSendRate, uint sendRate) : base(registry)
        {
            _serverSendData = serverSendData;
            this.remoteSendRate = remoteSendRate;
            this.sendRate = sendRate;
        }

        /// <summary>
        ///     The server's room instance. TODO: Multiple rooms?
        /// </summary>
        [CanBeNull]
        public RailServerRoom Room { get; set; }

        /// <summary>
        ///     Starts the server's room.
        /// </summary>
        [PublicAPI]
        public RailServerRoom StartRoom()
        {
            Room = new RailServerRoom(Resource);
            return Room;
        }

        /// <summary>
        ///     Wraps an incoming connection in a peer and stores it. To be called
        ///     from the network API wrapper.
        /// </summary>
        [PublicAPI]
        public RailServerPeer AddClient()
        {
            var client = new RailServerPeer(Resource);
            client.EventReceived += OnEventReceived;
            client.ProcessCommandUpdate += ProcessCommandUpdate;

            Room.AddClient(client);
            ClientAdded?.Invoke(client);
            return client;
        }


        protected void OnEventReceived(RailEvent evnt, RailServerPeer sender)
        {
            Room.HandleEvent(evnt, sender);
        }


        [PublicAPI] public event Action<RailServerPeer> ClientAdded;

        /// <summary>
        ///     Wraps an incoming connection in a peer and stores it.
        /// </summary>
        public void RemoveClient(RailServerPeer toRemove)
        {
            if (Room.RemoveClient(toRemove))
            {
                // Revoke control of all the entities controlled by that client
                toRemove.Shutdown();
                ClientRemoved?.Invoke(toRemove);
            }
        }

        [PublicAPI] public event Action<RailServerPeer> ClientRemoved;


        private RailPacketToClient _packetToClient = new RailPacketToClient();

        private RailInterpreter _interpreter = new RailInterpreter();

        private RailPacketFromClient _packetFromClient = new RailPacketFromClient();

        /// <summary>
        ///     Updates all entities and dispatches a snapshot if applicable. Should
        ///     be called once per game simulation tick (e.g. during Unity's
        ///     FixedUpdate pass).
        /// </summary>
        [PublicAPI]
        public void Update()
        {
            _stopWatch.Restart();

            DoStart();
            Room.TickUpdate();

            if (Room.Tick.IsSendTick(sendRate))
            {
                Room.StoreStates();

                var clientsCount = Room.Clients.Count;

                for (var index = 0; index < clientsCount; index++)
                {
                    var clientPeer = Room.Clients[index];

                    _packetToClient.Reset();

                    clientPeer.SendPacket(_packetToClient, Room.Tick, Room.Entities.Values, Room.RemovedEntities);

                    //if (_stopWatch.IsRunning &&
                       // clientPeer.LatestRemote.IsValid)
                    {

                        _packetToClient.PingProcessDelay = _stopWatch.Elapsed.TotalMilliseconds;
                        //_stopWatch.Stop();
                    }
                    //else
                    {
                       // _packetToClient.PingProcessDelay = 0;
                    }

                    var toSend = Interpreter.SendPacket(Resource, _packetToClient);
                    _serverSendData(clientPeer, toSend);
                }
            }

            Room.CleanRemovedEntities();
        }

        #region Packet Receive


        private void ProcessCommandUpdate(RailServerPeer peer, RailCommandUpdate update)
        {
            if (Room.TryGet(update.EntityId, out IServerCommandedEntity entity))
            {
                bool canReceive = entity.Controller == peer &&
                                  entity.IsRemoving() == false;

                if (canReceive)
                {
                    foreach (var command in update.Commands)
                    {
                        entity.ReceiveCommand(command);
                    }
                }
                else // Can't send commands to that entity, so dump them
                {
                    foreach (RailCommand command in update.Commands)
                    {
                        RailPool.Free(command);
                    }
                }
            }
        }
        #endregion

        private Stopwatch _stopWatch = new Stopwatch();
        private double _latestProcess;


        public void ProcessPeerData(RailServerPeer peer, ArraySegment<byte> segment, Stopwatch recieve)
        {
            try
            {
                var bitBuffer = _interpreter.LoadData(segment);

                _packetFromClient.Reset();
                _packetFromClient.Decode(Resource, bitBuffer);

                if (bitBuffer.IsFinished)
                {
                    _stopWatch.Restart();
                    _latestProcess = recieve.Elapsed.TotalMilliseconds;

                    peer.ProcessPacket(Room.Tick, _packetFromClient);
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
    }
}
