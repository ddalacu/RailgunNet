using Moq;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;
using Xunit;

namespace Tests
{
    public class RailEventTest
    {
        public RailEventTest()
        {
            eventCreator.Setup(f => f.CreateEvent(iFactoryType)).Returns(pool.Allocate());
            eventCreator.Setup(f => f.EventTypeCompressor)
                        .Returns(new RailIntCompressor(0, iFactoryType + 1));
        }

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

        private readonly RailMemoryPool<RailEvent> pool =
            new RailMemoryPool<RailEvent>(new RailFactory<Event>());

        private readonly Mock<IRailEventConstruction> eventCreator =
            TestUtils.EventConstructionMock();

        [Fact]
        private void EventDataIsSerialized()
        {
            Event instance0 = (Event) eventCreator.Object.CreateEvent(iFactoryType);
            instance0.Attempts = 1;
            instance0.Data = 43;
            instance0.EventId = SequenceId.Start + 44;
            eventCreator.Verify(m => m.CreateEvent(iFactoryType), Times.Once());

            // Encode to buffer
            RailBitBuffer buffer = new RailBitBuffer();
            instance0.Encode(eventCreator.Object.EventTypeCompressor, buffer, Tick.START);

            // Read from buffer
            RailEvent instance1Base = RailEvent.Decode(
                eventCreator.Object,
                eventCreator.Object.EventTypeCompressor,
                buffer,
                Tick.START);

            eventCreator.Verify(m => m.CreateEvent(iFactoryType), Times.Exactly(2));
            Assert.Equal(instance0.EventId, instance1Base.EventId);
            Assert.IsType<Event>(instance0);
            Assert.Equal(instance0.Data, ((Event) instance1Base).Data);
        }

        [Fact]
        private void EventSequenceComparatorsWrap()
        {
            var first = SequenceId.Start;
            var second = first.Next;
            for (int i = 0; i < 10000; ++i)
            {
                Assert.True(first < second);
                first = second;
                second = second.Next;
            }
        }
    }
}
