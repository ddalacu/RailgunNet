using RailgunNet.Logic;

namespace RailgunNet.Factory
{
    public interface IRailEventCreator
    {
        RailEvent CreateEvent(int iFactoryType);
    }
}