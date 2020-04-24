using System.Collections.Generic;
using Moq;
using RailgunNet.Connection;
using RailgunNet.Connection.Client;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System;
using RailgunNet.System.Types;
using Xunit;

namespace Tests
{
    public class RailPacketSerializerTest
    {
        private readonly List<RailCommandUpdate> commandUpdates = new List<RailCommandUpdate>();
        private readonly List<RailCommand> commands = new List<RailCommand>();
        private readonly List<RailEvent> events = new List<RailEvent>();
        Mock<IRailCommandConstruction> commandFactory = new Mock<IRailCommandConstruction>();
        public RailPacketSerializerTest()
        {
            commandFactory.Setup(f => f.CreateCommand()).Returns(new TestUtils.Command());
            commandFactory.Setup(f => f.CreateCommandUpdate()).Returns(new RailCommandUpdate());
        }

        [Fact]
        void VerifyEmptyPacketFromClientToServer()
        {

            //RailCommandUpdate commandUpdate = RailCommandUpdate.Create(commandFactory.Object, EntityId.START, commands);

            RailPacketToServer toServer = new RailPacketToServer();
            toServer.Initialize(Tick.START, Tick.START, SequenceId.Start, events);
            toServer.Populate(commandUpdates, new RailView());

            // RailPacketSerializer.Encode(toServer, );
        }
    }
}