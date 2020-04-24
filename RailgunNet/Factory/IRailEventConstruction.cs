using RailgunNet.Logic;
using RailgunNet.System.Encoding.Compressors;

namespace RailgunNet.Factory
{
    public interface IRailEventConstruction
    {
        RailEvent CreateEvent(int iFactoryType);
        RailIntCompressor EventTypeCompressor { get; }
    }
}