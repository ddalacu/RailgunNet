using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;

namespace Tests.Example
{
    public class Event : RailEvent<Event>
    {
        protected override void CopyDataFrom(Event other)
        {
        }

        protected override void ReadData(RailBitBuffer buffer, Tick packetTick)
        {
        }

        protected override void ResetData()
        {
        }

        protected override void WriteData(RailBitBuffer buffer, Tick packetTick)
        {
        }
    }
}
