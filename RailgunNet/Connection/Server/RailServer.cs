using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RailgunNet.Connection.Traffic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Types;
using RailgunNet.Util;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Connection.Server
{
    /// <summary>
    ///     Server is the core executing class on the server. It is responsible for
    ///     managing connection contexts and payload I/O.
    /// </summary>
    [PublicAPI]
    public class RailServer : RailConnection
    {
        private readonly uint remoteSendRate;
        private readonly uint sendRate;

        /// <summary>
        ///     Collection of all participating clients.
        /// </summary>
        private readonly Dictionary<IRailNetPeer, RailServerPeer> clients = new Dictionary<IRailNetPeer, RailServerPeer>();

        /// <summary>
        ///     Collection of all participating clients.
        /// </summary>
        [PublicAPI] [NotNull] public IReadOnlyCollection<RailServerPeer> ConnectedClients => clients.Values;

        public RailServer(RailRegistry<RailEntityServer> registry, uint remoteSendRate, uint sendRate) : base(registry)
        {
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
            Room = new RailServerRoom(Resource, this);
            SetRoom(Room, Tick.START);
            return Room;
        }

        /// <summary>
        ///     Wraps an incoming connection in a peer and stores it. To be called
        ///     from the network API wrapper.
        /// </summary>
        [PublicAPI]
        public bool AddClient(IRailNetPeer netPeer, string identifier, out RailServerPeer client)
        {
            if (clients.ContainsKey(netPeer) == false)
            {
                client = new RailServerPeer(Resource, netPeer, remoteSendRate, Interpreter)
                {
                    Identifier = identifier
                };
                client.EventReceived += OnEventReceived;
                client.PacketReceived += OnPacketReceived;
                clients.Add(netPeer, client);
                Room.AddClient(client);
                ClientAdded?.Invoke(client);
                return true;
            }

            client = default;
            return false;
        }

        [PublicAPI] public event Action<RailServerPeer> ClientAdded;

        /// <summary>
        ///     Wraps an incoming connection in a peer and stores it.
        /// </summary>
        [PublicAPI]
        public void RemoveClient(IRailNetPeer netClient)
        {
            if (clients.ContainsKey(netClient))
            {
                RailServerPeer client = clients[netClient];
                clients.Remove(netClient);
                Room.RemoveClient(client);

                // Revoke control of all the entities controlled by that client
                client.Shutdown();
                ClientRemoved?.Invoke(client);
            }
        }

        [PublicAPI] public event Action<RailServerPeer> ClientRemoved;

        /// <summary>
        ///     Updates all entities and dispatches a snapshot if applicable. Should
        ///     be called once per game simulation tick (e.g. during Unity's
        ///     FixedUpdate pass).
        /// </summary>
        [PublicAPI]
        public void Update()
        {
            DoStart();

            Room.UpdateClients();

            Room.ServerUpdate();

            if (Room.Tick.IsSendTick(sendRate))
            {
                Room.StoreStates();
                Room.BroadcastPackets();
            }

            Room.CleanRemovedEntities();
        }

        #region Packet Receive
        private void OnPacketReceived(RailServerPeer peer, IRailClientPacket packet)
        {
            foreach (RailCommandUpdate update in packet.CommandUpdates)
            {
                ProcessCommandUpdate(peer, update);
            }
        }

        private void ProcessCommandUpdate(RailServerPeer peer, RailCommandUpdate update)
        {
            if (Room.TryGet(update.EntityId, out RailEntityServer entity))
            {
                bool canReceive = entity.Controller == peer && entity.IsRemoving == false;

                if (canReceive)
                {
                    foreach (RailCommand command in update.Commands)
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
    }
}
