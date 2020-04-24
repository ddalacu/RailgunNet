using RailgunNet.Logic;
using RailgunNet.System.Encoding;

namespace Tests
{
    public class TestUtils
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
    }
}