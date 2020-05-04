using Moq;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.System.Types;
using Xunit;

namespace Tests
{
    public class RailEventTest
    {
        private const int iFactoryType = 7;
        private class Event : RailEvent
        {
            public Event()
            {
                FactoryType = iFactoryType; // Usually done by RailResource
            }

            [EventData] public int Data { get; set; }
            protected override void Execute(RailRoom room, RailController sender)
            {
            }
        }

        private readonly Mock<IRailEventConstruction> eventCreator =
            TestUtils.EventConstructionMock();

        public RailEventTest()
        {
            eventCreator.Setup(f => f.CreateEvent(iFactoryType)).Returns(new Event());
            eventCreator.Setup(f => f.EventTypeCompressor).Returns(new RailIntCompressor(0, iFactoryType + 1));
        }

        [Fact]
        void EventDataIsSerialized()
        {
            Event instance0 = new Event
            {
                Attempts = 1,
                Data = 43,
                EventId = SequenceId.Start + 44
            };

            // Encode to buffer
            RailBitBuffer buffer = new RailBitBuffer();
            instance0.Encode(eventCreator.Object.EventTypeCompressor, buffer, Tick.START);

            // Read from buffer
            RailEvent instance1Base = RailEvent.Decode(
                eventCreator.Object,
                eventCreator.Object.EventTypeCompressor,
                buffer,
                Tick.START);

            eventCreator.Verify(m=>m.CreateEvent(iFactoryType), Times.Once());
            Assert.Equal(instance0.EventId, instance1Base.EventId);
            Assert.IsType<Event>(instance0);
            Assert.Equal(instance0.Data, ((Event)instance1Base).Data);
        }
    }
}
