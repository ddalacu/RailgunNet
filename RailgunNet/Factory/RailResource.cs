using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Factory
{
    public class RailResource
        : IRailCommandConstruction, IRailEventConstruction, IRailStateConstruction
    {
        [CanBeNull] private readonly IRailMemoryPool<RailCommand> commandPool;

        private readonly IRailMemoryPool<RailCommandUpdate> commandUpdatePool;

        private readonly IRailMemoryPool<RailStateDelta> deltaPool;
        private readonly Dictionary<int, IRailMemoryPool<RailEntityBase>> entityPools;

        private readonly Dictionary<Type, int> entityTypeToKey;
        private readonly Dictionary<int, IRailMemoryPool<RailEvent>> eventPools;
        private readonly Dictionary<Type, int> eventTypeToKey;

        [CanBeNull]
        private readonly IRailMemoryPool<RailStateRecord> recordPool;

        private readonly Dictionary<int, IRailMemoryPool<RailState>> statePools;

        public RailResource(RailRegistry registry)
        {
            entityTypeToKey = new Dictionary<Type, int>();
            eventTypeToKey = new Dictionary<Type, int>();

            commandPool = CreateCommandPool(registry);
            entityPools = new Dictionary<int, IRailMemoryPool<RailEntityBase>>();
            statePools = new Dictionary<int, IRailMemoryPool<RailState>>();
            eventPools = new Dictionary<int, IRailMemoryPool<RailEvent>>();

            RegisterEvents(registry);
            RegisterEntities(registry);

            EventTypeCompressor = new RailIntCompressor(0, eventPools.Count + 1);
            EntityTypeCompressor = new RailIntCompressor(0, entityPools.Count + 1);

            deltaPool = new RailMemoryPool<RailStateDelta>(new RailFactory<RailStateDelta>());
            commandUpdatePool =
                new RailMemoryPool<RailCommandUpdate>(new RailFactory<RailCommandUpdate>());

            if (registry is RailRegistry<RailEntityServer>)
            {
                recordPool = new RailMemoryPool<RailStateRecord>(new RailFactory<RailStateRecord>());
            }
        }

        public RailIntCompressor EventTypeCompressor { get; }
        public RailIntCompressor EntityTypeCompressor { get; }

        private static IRailMemoryPool<RailCommand> CreateCommandPool(RailRegistry registry)
        {
            return registry.CommandType == null ?
                null :
                new RailMemoryPool<RailCommand>(new RailFactory<RailCommand>(registry.CommandType));
        }

        private void RegisterEvents(RailRegistry registry)
        {
            foreach (EventConstructionInfo eventInfo in registry.EventTypes)
            {
                IRailMemoryPool<RailEvent> statePool = new RailMemoryPool<RailEvent>(
                    new RailFactory<RailEvent>(eventInfo.Type, eventInfo.ConstructorParams));

                int typeKey = eventPools.Count + 1; // 0 is an invalid type
                eventPools.Add(typeKey, statePool);
                eventTypeToKey.Add(eventInfo.Type, typeKey);
            }
        }

        private void RegisterEntities(RailRegistry registry)
        {
            foreach (EntityConstructionInfo pair in registry.EntityTypes)
            {
                IRailMemoryPool<RailState> statePool = new RailMemoryPool<RailState>(
                    new RailFactory<RailState>(pair.State));
                IRailMemoryPool<RailEntityBase> entityPool = new RailMemoryPool<RailEntityBase>(
                    new RailFactory<RailEntityBase>(pair.Entity, pair.ConstructorParamsEntity));

                int typeKey = statePools.Count + 1; // 0 is an invalid type
                statePools.Add(typeKey, statePool);
                entityPools.Add(typeKey, entityPool);
                entityTypeToKey.Add(pair.Entity, typeKey);
            }
        }

        #region Allocation
        public RailCommand CreateCommand()
        {
            return commandPool.Allocate();
        }

        public RailEntityBase CreateEntity(int factoryType)
        {
            return entityPools[factoryType].Allocate();
        }

        public RailState CreateState(int factoryType)
        {
            RailState state = statePools[factoryType].Allocate();
            state.FactoryType = factoryType;
            return state;
        }

        public RailEvent CreateEvent(int factoryType)
        {
            RailEvent instance = eventPools[factoryType].Allocate();
            instance.FactoryType = factoryType;
            return instance;
        }

        public T CreateEvent<T>()
            where T : RailEvent
        {
            return (T)CreateEvent(eventTypeToKey[typeof(T)]);
        }

        public RailStateDelta CreateDelta()
        {
            return deltaPool.Allocate();
        }

        public RailCommandUpdate CreateCommandUpdate()
        {
            return commandUpdatePool.Allocate();
        }

        public RailStateRecord CreateRecord()
        {
            return recordPool?.Allocate();
        }

        #region Typed
        public int GetEntityFactoryType<T>()
            where T : RailEntityBase
        {
            return entityTypeToKey[typeof(T)];
        }

        public int GetEventFactoryType<T>()
            where T : RailEvent
        {
            return eventTypeToKey[typeof(T)];
        }
        #endregion
        #endregion
    }
}
