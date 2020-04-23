using System;
using Moq;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using Xunit;

namespace Tests
{
    public class RailCommandTest
    {
        public class Command : RailCommand<Command>
        {
            public int Data;
            private readonly int initialData;

            public Command() : this(0)
            {

            }
            public Command(int i)
            {
                Data = i;
                initialData = i;
            }
            protected override void ReadData(RailBitBuffer buffer)
            {
                Data = buffer.ReadInt();
            }
            protected override void WriteData(RailBitBuffer buffer)
            {
                buffer.WriteInt(Data);
            }
            protected override void ResetData()
            {
                Data = initialData;
            }
            protected override void CopyDataFrom(Command other)
            {
                Data = other.Data;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(Int32.MinValue)]
        [InlineData(Int32.MaxValue)]
        void EncodeWritesTickAndCommandData(int iData)
        {
            RailBitBuffer bitBuffer = new RailBitBuffer(2);
            Command command = new Command(iData)
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
            var mockCreator = new Mock<IRailCommandCreator>();
            mockCreator.Setup(m => m.CreateCommand()).Returns(new Command());

            RailBitBuffer bitBuffer = new RailBitBuffer(2);
            Tick writtenTick = Tick.START.GetNext();

            bitBuffer.WriteTick(writtenTick);
            bitBuffer.WriteInt(iData);

            RailCommand decodedGenericCommand = RailCommand.Decode(mockCreator.Object, bitBuffer);
            Assert.IsType<Command>(decodedGenericCommand);
            Command decodedCommand = decodedGenericCommand as Command;
            Assert.NotNull(decodedCommand);
            Assert.Equal(writtenTick, decodedCommand.ClientTick);
            Assert.Equal(iData, decodedCommand.Data);
        }
    }
}