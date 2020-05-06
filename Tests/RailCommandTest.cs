using Moq;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;
using Xunit;

namespace Tests
{
    public class RailCommandTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        private void EncodeWritesTickAndCommandData(int iData)
        {
            RailBitBuffer bitBuffer = new RailBitBuffer(2);
            TestUtils.Command command = new TestUtils.Command(iData)
            {
                ClientTick = Tick.START,
                IsNewCommand = true
            };
            ((IRailPoolable<RailCommand>) command).Allocated();
            command.Encode(bitBuffer);

            Tick writtenTick = bitBuffer.ReadTick();
            int writtenData = bitBuffer.ReadInt();
            Assert.Equal(Tick.START, writtenTick);
            Assert.Equal(iData, writtenData);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        private void DecodeReadsTickAndCommandData(int iData)
        {
            RailMemoryPool<RailCommand> pool =
                new RailMemoryPool<RailCommand>(new RailFactory<TestUtils.Command>());
            Mock<IRailCommandConstruction> mockCreator = new Mock<IRailCommandConstruction>();
            mockCreator.Setup(m => m.CreateCommand()).Returns(pool.Allocate());

            RailBitBuffer bitBuffer = new RailBitBuffer(2);
            Tick writtenTick = Tick.START.GetNext();

            bitBuffer.WriteTick(writtenTick);
            bitBuffer.WriteInt(iData);

            RailCommand decodedGenericCommand = RailCommand.Decode(mockCreator.Object, bitBuffer);
            Assert.IsType<TestUtils.Command>(decodedGenericCommand);
            TestUtils.Command decodedCommand = decodedGenericCommand as TestUtils.Command;
            Assert.NotNull(decodedCommand);
            Assert.Equal(writtenTick, decodedCommand.ClientTick);
            Assert.Equal(iData, decodedCommand.Data);
        }
    }
}
