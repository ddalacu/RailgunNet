using System.Collections.Generic;
using Moq;
using RailgunNet.Connection;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using Xunit;
using Newtonsoft.Json;

namespace Tests
{
    public class RailPacketTest
    {
        private readonly RailBitBuffer bitBuffer = new RailBitBuffer(2);
        private readonly Mock<IRailCommandConstruction> commandCreator = TestUtils.CommandConstructionMock();
        private readonly Mock<IRailEventConstruction> eventCreator = TestUtils.EventConstructionMock();
        private readonly Mock<IRailStateConstruction> stateCreator = TestUtils.StateConstructionMock();
        public RailPacketTest()
        {
        }

        [Fact]
        void VerifyEmptyPacketFromClientToServer()
        {
            // Init
            RailPacketToServer toServer = new RailPacketToServer();
            Tick startingTick = TestUtils.CreateTick(4200);
            Tick lastAckTick = TestUtils.CreateTick(4200 - 5);
            toServer.Initialize(startingTick, lastAckTick, SequenceId.Start, new List<RailEvent>());
            toServer.Populate(new List<RailCommandUpdate>(), new RailView());

            // Verify initialize
            Assert.Equal<Tick>(startingTick, toServer.SenderTick);
            Assert.Equal<Tick>(lastAckTick, toServer.LastAckTick);

            // Encode
            Assert.True(bitBuffer.Empty);
            toServer.Encode(stateCreator.Object, eventCreator.Object, bitBuffer);

            // Since the packet was empty, nothing was allocated
            stateCreator.Verify(f=>f.CreateState(It.IsAny<int>()), Times.Never);
            stateCreator.Verify(f=>f.CreateDelta(), Times.Never);
            stateCreator.Verify(f=>f.CreateRecord(), Times.Never);
            stateCreator.Verify(f=>f.EntityTypeCompressor, Times.Never);
            eventCreator.Verify(f=>f.CreateEvent(It.IsAny<int>()), Times.Never);
            eventCreator.Verify(f=>f.EventTypeCompressor, Times.Never);

            // Bitbuffer was written to.
            Assert.True(!bitBuffer.Empty);

            // Decode
            RailPacketFromClient fromClient = new RailPacketFromClient();
            fromClient.Decode(commandCreator.Object, stateCreator.Object, eventCreator.Object, bitBuffer);

            // Verify decoded packet
            Assert.True(bitBuffer.IsFinished);
            Assert.Equal<RailPacketBase>(toServer, fromClient, new TestUtils.RailPacketComparer());

            // Since the packet was empty, nothing was allocated
            stateCreator.Verify(f => f.CreateState(It.IsAny<int>()), Times.Never);
            stateCreator.Verify(f => f.CreateDelta(), Times.Never);
            stateCreator.Verify(f => f.CreateRecord(), Times.Never);
            stateCreator.Verify(f => f.EntityTypeCompressor, Times.Never);
            eventCreator.Verify(f => f.CreateEvent(It.IsAny<int>()), Times.Never);
            eventCreator.Verify(f => f.EventTypeCompressor, Times.Never);
        }
        [Fact]
        void VerifyEmptyPacketFromServerToClient()
        {
            // Init
            RailPacketToClient toClient = new RailPacketToClient();
            Tick startingTick = TestUtils.CreateTick(4200);
            Tick lastAckTick = TestUtils.CreateTick(4200 - 5);
            toClient.Initialize(startingTick, lastAckTick, SequenceId.Start, new List<RailEvent>());
            toClient.Populate(new List<RailStateDelta>(), new List<RailStateDelta>(), new List<RailStateDelta>());

            // Verify initialize
            Assert.Equal<Tick>(startingTick, toClient.SenderTick);
            Assert.Equal<Tick>(lastAckTick, toClient.LastAckTick);

            // Encode
            Assert.True(bitBuffer.Empty);
            toClient.Encode(stateCreator.Object, eventCreator.Object, bitBuffer);

            // Since the packet was empty, nothing was allocated
            stateCreator.Verify(f => f.CreateState(It.IsAny<int>()), Times.Never);
            stateCreator.Verify(f => f.CreateDelta(), Times.Never);
            stateCreator.Verify(f => f.CreateRecord(), Times.Never);
            stateCreator.Verify(f => f.EntityTypeCompressor, Times.Never);
            eventCreator.Verify(f => f.CreateEvent(It.IsAny<int>()), Times.Never);
            eventCreator.Verify(f => f.EventTypeCompressor, Times.Never);

            // Bitbuffer was written to.
            Assert.True(!bitBuffer.Empty);

            // Decode
            RailPacketFromServer fromServer = new RailPacketFromServer();
            fromServer.Decode(commandCreator.Object, stateCreator.Object, eventCreator.Object, bitBuffer);

            // Verify decoded packet
            Assert.True(bitBuffer.IsFinished);
            Assert.Equal<RailPacketBase>(toClient, fromServer, new TestUtils.RailPacketComparer());

            // Since the packet was empty, nothing was allocated
            stateCreator.Verify(f => f.CreateState(It.IsAny<int>()), Times.Never);
            stateCreator.Verify(f => f.CreateDelta(), Times.Never);
            stateCreator.Verify(f => f.CreateRecord(), Times.Never);
            stateCreator.Verify(f => f.EntityTypeCompressor, Times.Never);
            eventCreator.Verify(f => f.CreateEvent(It.IsAny<int>()), Times.Never);
            eventCreator.Verify(f => f.EventTypeCompressor, Times.Never);
        }
    }
}