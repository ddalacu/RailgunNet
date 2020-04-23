using RailgunNet.Logic;

namespace RailgunNet.Factory
{
    public interface IEventCreator
    {
        RailEvent CreateEvent(int iFactoryType);
    }
}