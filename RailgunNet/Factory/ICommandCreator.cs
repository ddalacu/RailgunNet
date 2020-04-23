using RailgunNet.Logic;

namespace RailgunNet.Factory
{
    public interface ICommandCreator
    {
        RailCommand CreateCommand();
    }
}