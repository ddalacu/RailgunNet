using RailgunNet.Logic;

namespace RailgunNet.Factory
{
    public interface IRailCommandCreator
    {
        RailCommand CreateCommand();
    }
}