using RailgunNet;
using RailgunNet.Factory;

namespace Tests.Example
{
    public static class Registry
    {
        public static RailRegistry Get(Component eComponent)
        {
            RailRegistry registry = new RailRegistry(eComponent);
            registry.SetCommandType<Command>();
            registry.AddEventType<Event>();
            return registry;
        }
    }
}
