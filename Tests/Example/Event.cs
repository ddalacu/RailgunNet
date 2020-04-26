using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;

namespace Tests.Example
{
    public class Event : RailEvent<Event>
    {
        protected override void CopyDataFrom(Event other)
        {
            throw new System.NotImplementedException();
        }

        protected override void ReadData(RailBitBuffer buffer, Tick packetTick)
        {
            throw new System.NotImplementedException();
        }

        protected override void ResetData()
        {
            throw new System.NotImplementedException();
        }

        protected override void WriteData(RailBitBuffer buffer, Tick packetTick)
        {
            throw new System.NotImplementedException();
        }
    }
}
