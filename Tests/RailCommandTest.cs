using System;
using Moq;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using Xunit;

namespace Tests
{
    public partial class RailCommandTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(Int32.MinValue)]
        [InlineData(Int32.MaxValue)]
        void EncodeWritesTickAndCommandData(int iData)
        {
            RailBitBuffer bitBuffer = new RailBitBuffer(2);
            TestUtils.Command command = new TestUtils.Command(iData)
            {
                ClientTick = Tick.START, 
                IsNewCommand = true
            };
            command.Encode(bitBuffer);

            Tick writtenTick = bitBuffer.ReadTick();
            int writtenData = bitBuffer.ReadInt();
            Assert.Equal(Tick.START, writtenTick);
            Assert.Equal(iData, writtenData);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(Int32.MinValue)]
        [InlineData(Int32.MaxValue)]
        void DecodeReadsTickAndCommandData(int iData)
        {
            var mockCreator = new Mock<IRailCommandConstruction>();
            mockCreator.Setup(m => m.CreateCommand()).Returns(new TestUtils.Command());

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