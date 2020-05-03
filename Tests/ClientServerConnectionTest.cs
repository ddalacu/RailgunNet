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

        private class EntityState
        {
            [Mutable] public int EntityId { get; set; }
            [Mutable] [Compressor(typeof(CoordinateCompressor))] public float PosX { get; set; }
            [Mutable] [Compressor(typeof(CoordinateCompressor))] public float PosY { get; set; }
        }

        private class Command : RailCommand<Command>
        {
            private readonly CoordinateCompressor coordinateCompressor = new CoordinateCompressor();
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
                    buffer.ReadFloat(coordinateCompressor),
                    buffer.ReadFloat(coordinateCompressor));
            }

            protected override void ResetData()
            {
                SetData(0, 0);
            }

            protected override void EncodeData(RailBitBuffer buffer)
            {
                buffer.WriteFloat(coordinateCompressor, PosX);
                buffer.WriteFloat(coordinateCompressor, PosY);
            }
        }

        private class EntityClient : RailEntityClient<RailStateGeneric<EntityState>>
        {
        }

        private class EntityServer : RailEntityServer<RailStateGeneric<EntityState>>
        {
        }

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
            Assert.Equal(entityClientSide.State.Data.PosX, entityServerSide.State.Data.PosX);
            Assert.Equal(entityClientSide.State.Data.PosY, entityServerSide.State.Data.PosY);

            // Change the entity on the server side and sync it to the client
            float fExpectedPosX = 42;
            float fExpectedPosY = 106;
            entityServerSide.State.Data.PosX = fExpectedPosX;
            entityServerSide.State.Data.PosY = fExpectedPosY;

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

            Assert.Equal(fExpectedPosX, entityClientSide.State.Data.PosX);
            Assert.Equal(fExpectedPosY, entityClientSide.State.Data.PosY);
            Assert.Equal(fExpectedPosX, entityServerSide.State.Data.PosX);
            Assert.Equal(fExpectedPosY, entityServerSide.State.Data.PosY);
        }
    }
}
