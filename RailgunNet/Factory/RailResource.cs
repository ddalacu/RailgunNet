using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.Util.Pooling;
using UnityEngine;

namespace RailgunNet.Factory
{
    public enum EntityType : int
    {

    }
    public enum EventType : int
    {

    }


    public class RailResource : IRailCommandConstruction, IRailEventConstruction, IRailStateConstruction
    {
        [CanBeNull] private readonly IRailMemoryPool<RailCommand> commandPool;

        private readonly IRailMemoryPool<RailCommandUpdate> commandUpdatePool;

        private readonly IRailMemoryPool<RailStateDelta> deltaPool;
        private readonly Dictionary<int, IRailMemoryPool<IEntity>> entityPools;

        private readonly Dictionary<Type, int> entityTypeToKey;
        private readonly Dictionary<Type, int> stateTypeToKey;

        private readonly Dictionary<int, IRailMemoryPool<RailEvent>> eventPools;
        private readonly Dictionary<Type, int> eventTypeToKey;

        [CanBeNull]
        private readonly IRailMemoryPool<RailStateRecord> recordPool;

        private readonly Dictionary<int, IRailMemoryPool<RailState>> statePools;

        public RailResource(RailRegistry registry)
        {
            entityTypeToKey = new Dictionary<Type, int>();
            eventTypeToKey = new Dictionary<Type, int>();
            stateTypeToKey = new Dictionary<Type, int>();

            commandPool = CreateCommandPool(registry);
            entityPools = new Dictionary<int, IRailMemoryPool<IEntity>>();
            statePools = new Dictionary<int, IRailMemoryPool<RailState>>();
            eventPools = new Dictionary<int, IRailMemoryPool<RailEvent>>();

            RegisterEvents(registry);
            RegisterEntities(registry);

            EventTypeCompressor = new RailIntCompressor(0, eventPools.Count + 1);
            EntityTypeCompressor = new RailIntCompressor(0, entityPools.Count + 1);

            deltaPool = new RailMemoryPool<RailStateDelta>(new RailFactory<RailStateDelta>());
            commandUpdatePool =
                new RailMemoryPool<RailCommandUpdate>(new RailFactory<RailCommandUpdate>());

            if (registry is RailRegistry<IServerEntity>)
            {
                recordPool = new RailMemoryPool<RailStateRecord>(new RailFactory<RailStateRecord>());
            }
        }

        public RailIntCompressor EventTypeCompressor { get; }

        public EntityType GetEntityType(RailState state)
        {
            return (EntityType)stateTypeToKey[state.GetType()];
        }

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
                IRailMemoryPool<IEntity> entityPool = new RailMemoryPool<IEntity>(
                    new RailFactory<IEntity>(pair.Entity, pair.ConstructorParamsEntity));

                int typeKey = statePools.Count + 1; // 0 is an invalid type

                statePools.Add(typeKey, statePool);
                entityPools.Add(typeKey, entityPool);

                entityTypeToKey.Add(pair.Entity, typeKey);
                stateTypeToKey.Add(pair.State, typeKey);
            }
        }

        #region Allocation
        public RailCommand CreateCommand()
        {
            return commandPool.Allocate();
        }

        public IEntity CreateEntity(EntityType factoryType)
        {
            var allocate = entityPools[(int)factoryType].Allocate();
            allocate.InitState(this);
            return allocate;
        }

        public T CreateEntity<T>() where T : IEntity
        {
            var factoryType = GetEntityFactoryType<T>();

            var allocate = entityPools[(int)factoryType].Allocate();
            allocate.InitState(this);
            return (T)allocate;
        }

        public RailState CreateState(EntityType factoryType)
        {
            RailState state = statePools[(int)factoryType].Allocate();
            return state;
        }

        public RailEvent CreateEvent(EventType factoryType)
        {
            RailEvent instance = eventPools[(int)factoryType].Allocate();
            instance.FactoryType = factoryType;
            return instance;
        }

        public T CreateEvent<T>()
            where T : RailEvent
        {
            return (T)CreateEvent((EventType)eventTypeToKey[typeof(T)]);
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
        public EntityType GetEntityFactoryType<T>()
            where T : IEntity
        {
            return (EntityType)entityTypeToKey[typeof(T)];
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
