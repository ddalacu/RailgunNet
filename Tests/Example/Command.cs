using RailgunNet.Logic;
using RailgunNet.System.Encoding;

namespace Tests.Example
{
    public class Command : RailCommand<Command>
    {
        protected override void CopyDataFrom(Command other)
        {
            throw new System.NotImplementedException();
        }

        protected override void ReadData(RailBitBuffer buffer)
        {
            throw new System.NotImplementedException();
        }

        protected override void ResetData()
        {
            throw new System.NotImplementedException();
        }

        protected override void WriteData(RailBitBuffer buffer)
        {
            throw new System.NotImplementedException();
        }
    }
}
