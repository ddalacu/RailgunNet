using RailgunNet;
using RailgunNet.Factory;

namespace Tests.Example
{
    public static class Registry
    {
        public static RailRegistry Get(Component eComponent)
        {
            RailRegistry registry = new RailRegistry(eComponent);
            switch (eComponent)
            {
                case Component.Server:
                    registry.AddEntityType<EntityServer, EntityState>();
                    break;
                case Component.Client:
                    registry.AddEntityType<EntityClient, EntityState>();
                    break;
            }
            registry.SetCommandType<Command>();
            registry.AddEventType<Event>();
            return registry;
        }
    }
}
