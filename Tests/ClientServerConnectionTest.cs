using System;
using System.Linq;
using Moq;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.State;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using Xunit;
using static Tests.Example.Util;

namespace Tests.Example
{
    public class ClientServerConnectionTest
    {
        public ClientServerConnectionTest()
        {
            peerClientSide = new Mock<InMemoryNetPeerWrapper>
            {
                CallBase = true
            };
            peerServerSide = new Mock<InMemoryNetPeerWrapper>
            {
                CallBase = true
            };
            peerClientSide.Object.OnSendPayload += peerServerSide.Object.ReceivePayload;
            peerServerSide.Object.OnSendPayload += peerClientSide.Object.ReceivePayload;
        }

        private readonly RailClient client = new RailClient(Registry.Get(Component.Client));
        private readonly RailServer server = new RailServer(Registry.Get(Component.Server));

        private readonly Mock<InMemoryNetPeerWrapper> peerClientSide;
        private readonly Mock<InMemoryNetPeerWrapper> peerServerSide;

        [Fact]
        private void EntitiesAreSynchronized()
        {
            // Initialization
            RailClientRoom clientRoom = client.StartRoom();
            RailServerRoom serverRoom = server.StartRoom();
            EntityServer entityServerSide = serverRoom.AddNewEntity<EntityServer>();
            server.AddClient(peerServerSide.Object, "");
            client.SetPeer(peerClientSide.Object);

            // Nothing has been sent or received yet
            peerClientSide.Verify(
                c => c.SendPayload(It.IsAny<ArraySegment<byte>>()),
                Times.Never());
            peerServerSide.Verify(
                c => c.SendPayload(It.IsAny<ArraySegment<byte>>()),
                Times.Never());
            Assert.Empty(clientRoom.Entities);
            Assert.Single(serverRoom.Entities);

            // Let the server send its update to the client
            for (int i = 0; i < RailConfig.SERVER_SEND_RATE + RailConfig.CLIENT_SEND_RATE + 1; ++i)
            {
                peerServerSide.Object.PollEvents();
                server.Update();
                peerClientSide.Object.PollEvents();
                client.Update();
            }

            // The client has received the server entity.
            Assert.Single(clientRoom.Entities);
            Assert.Single(serverRoom.Entities);

            // Clients representation of the entity is identical to the server
            IRailEntity entityProxy = clientRoom.Entities.First();
            Assert.IsType<EntityClient>(entityProxy);
            EntityClient entityClientSide = entityProxy as EntityClient;
            Assert.NotNull(entityClientSide);
            Assert.Equal(entityServerSide.Id, entityProxy.Id);
            Assert.Equal(entityClientSide.State.PosX, entityServerSide.State.PosX);
            Assert.Equal(entityClientSide.State.PosY, entityServerSide.State.PosY);

            // Change the entity on the server side and sync it to the client
            float fExpectedPosX = 42;
            float fExpectedPosY = 106;
            entityServerSide.State.PosX = fExpectedPosX;
            entityServerSide.State.PosY = fExpectedPosY;

            // Let the server detect the change and send the packet
            server.Update();
            bool bWasSendTick = serverRoom.Tick.IsSendTick(RailConfig.SERVER_SEND_RATE);
            while (!bWasSendTick)
            {
                server.Update();
                bWasSendTick = serverRoom.Tick.IsSendTick(RailConfig.SERVER_SEND_RATE);
            }

            // Let the client receive & process the packet. We need to bring the client up to the same tick as the server to see the result.
            while (clientRoom.Tick < serverRoom.Tick)
            {
                peerClientSide.Object.PollEvents();
                client.Update();
            }

            Assert.Equal(fExpectedPosX, entityClientSide.State.PosX);
            Assert.Equal(fExpectedPosY, entityClientSide.State.PosY);
            Assert.Equal(fExpectedPosX, entityServerSide.State.PosX);
            Assert.Equal(fExpectedPosY, entityServerSide.State.PosY);
        }
        private static class Registry
        {
            public static RailRegistry Get(Component eComponent)
            {
                RailRegistry registry = new RailRegistry(eComponent);
                switch (eComponent)
                {
                    case Component.Server:
                        registry.AddEntityType<EntityServer, EntityState>();
                        break;
                    case Component.Client:
                        registry.AddEntityType<EntityClient, EntityState>();
                        break;
                }

                registry.SetCommandType<Command>();
                return registry;
            }
        }
        private class EntityState : RailState<EntityState>
        {
            public int EntityId;
            public float PosX;
            public float PosY;

            public override void ResetAllData()
            {
                EntityId = 0;
                PosX = 0.0f;
                PosY = 0.0f;
            }

            #region Mutable
            [Flags]
            private enum Flags : uint
            {
                None = 0x00,
                PosX = 0x01,
                PosY = 0x02,
                All = PosX | PosY
            }

            protected override int FlagBits => (int)Flags.All;

            public override void ApplyMutableFrom(EntityState source, uint uFlags)
            {
                Flags flag = (Flags)uFlags;
                if (flag.HasFlag(Flags.PosX)) PosX = source.PosX;
                if (flag.HasFlag(Flags.PosY)) PosY = source.PosY;
            }

            public override uint CompareMutableData(EntityState other)
            {
                return (uint)((GameMath.CoordinatesEqual(PosX, other.PosX) ? Flags.None : Flags.PosX) |
                               (GameMath.CoordinatesEqual(PosY, other.PosY) ? Flags.None : Flags.PosY));
            }

            public override void DecodeMutableData(RailBitBuffer buffer, uint uFlags)
            {
                Flags flag = (Flags)uFlags;
                if (flag.HasFlag(Flags.PosX)) PosX = buffer.ReadFloat(Compression.Coordinate);
                if (flag.HasFlag(Flags.PosY)) PosY = buffer.ReadFloat(Compression.Coordinate);
            }

            public override void EncodeMutableData(RailBitBuffer buffer, uint uFlags)
            {
                Flags flag = (Flags)uFlags;
                if (flag.HasFlag(Flags.PosX)) buffer.WriteFloat(Compression.Coordinate, PosX);
                if (flag.HasFlag(Flags.PosY)) buffer.WriteFloat(Compression.Coordinate, PosY);
            }
            #endregion

            #region Immutable
            public override void ApplyImmutableFrom(EntityState source)
            {
                EntityId = source.EntityId;
            }

            public override void DecodeImmutableData(RailBitBuffer buffer)
            {
                EntityId = buffer.ReadInt();
            }

            public override void EncodeImmutableData(RailBitBuffer buffer)
            {
                buffer.WriteInt(EntityId);
            }
            #endregion

            #region Controller
            public override void ApplyControllerFrom(EntityState source)
            {
            }

            public override void DecodeControllerData(RailBitBuffer buffer)
            {
            }

            public override void EncodeControllerData(RailBitBuffer buffer)
            {
            }

            public override bool IsControllerDataEqual(EntityState basis)
            {
                return true;
            }

            public override void ResetControllerData()
            {
            }
            #endregion
        }

        private class Command : RailCommand<Command>
        {
            public float PosX;
            public float PosY;

            public void SetData(float X, float Y)
            {
                PosX = X;
                PosY = Y;
            }

            protected override void CopyDataFrom(Command other)
            {
                SetData(other.PosX, other.PosY);
            }

            protected override void DecodeData(RailBitBuffer buffer)
            {
                SetData(
                    buffer.ReadFloat(Compression.Coordinate),
                    buffer.ReadFloat(Compression.Coordinate));
            }

            protected override void ResetData()
            {
                SetData(0, 0);
            }

            protected override void EncodeData(RailBitBuffer buffer)
            {
                buffer.WriteFloat(Compression.Coordinate, PosX);
                buffer.WriteFloat(Compression.Coordinate, PosY);
            }
        }

        private class EntityClient : RailEntityClient<EntityState>
        {
        }

        private class EntityServer : RailEntityServer<EntityState>
        {
        }
    }
}
