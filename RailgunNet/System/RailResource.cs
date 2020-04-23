/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Railgun
{
    public class RailResource
    {
        private readonly IRailMemoryPool<RailCommand> commandPool;
        private readonly IRailMemoryPool<RailCommandUpdate> commandUpdatePool;

        private readonly IRailMemoryPool<RailStateDelta> deltaPool;
        private readonly Dictionary<int, IRailMemoryPool<RailEntity>> entityPools;

        private readonly Dictionary<Type, int> entityTypeToKey;
        private readonly Dictionary<int, IRailMemoryPool<RailEvent>> eventPools;
        private readonly Dictionary<Type, int> eventTypeToKey;

        [OnlyIn(Component.Server)] [CanBeNull] private readonly IRailMemoryPool<RailStateRecord> recordPool;

        private readonly Dictionary<int, IRailMemoryPool<RailState>> statePools;

        public RailResource(RailRegistry registry)
        {
            entityTypeToKey = new Dictionary<Type, int>();
            eventTypeToKey = new Dictionary<Type, int>();

            commandPool = CreateCommandPool(registry);
            entityPools = new Dictionary<int, IRailMemoryPool<RailEntity>>();
            statePools = new Dictionary<int, IRailMemoryPool<RailState>>();
            eventPools = new Dictionary<int, IRailMemoryPool<RailEvent>>();

            RegisterEvents(registry);
            RegisterEntities(registry);

            EventTypeCompressor =
                new RailIntCompressor(0, eventPools.Count + 1);
            EntityTypeCompressor =
                new RailIntCompressor(0, entityPools.Count + 1);

            deltaPool = new RailMemoryPool<RailStateDelta>(new RailFactory<RailStateDelta>());
            commandUpdatePool = new RailMemoryPool<RailCommandUpdate>(new RailFactory<RailCommandUpdate>());

            if (registry.Component == Component.Server)
                recordPool = new RailMemoryPool<RailStateRecord>(new RailFactory<RailStateRecord>());
        }

        public RailIntCompressor EventTypeCompressor { get; }
        public RailIntCompressor EntityTypeCompressor { get; }

        private IRailMemoryPool<RailCommand> CreateCommandPool(
            RailRegistry registry)
        {
            return new RailMemoryPool<RailCommand>(new RailFactory<RailCommand>(registry.CommandType));
        }

        private void RegisterEvents(
            RailRegistry registry)
        {
            foreach (Type eventType in registry.EventTypes)
            {
                IRailMemoryPool<RailEvent> statePool =
                    new RailMemoryPool<RailEvent>(new RailFactory<RailEvent>(eventType));

                int typeKey = eventPools.Count + 1; // 0 is an invalid type
                eventPools.Add(typeKey, statePool);
                eventTypeToKey.Add(eventType, typeKey);
            }
        }

        private void RegisterEntities(
            RailRegistry registry)
        {
            foreach (KeyValuePair<Type, Type> pair in registry.EntityTypes)
            {
                Type entityType = pair.Key;
                Type stateType = pair.Value;

                IRailMemoryPool<RailState> statePool =
                    new RailMemoryPool<RailState>(new RailFactory<RailState>(stateType));
                IRailMemoryPool<RailEntity> entityPool =
                    new RailMemoryPool<RailEntity>(new RailFactory<RailEntity>(entityType));

                int typeKey = statePools.Count + 1; // 0 is an invalid type
                statePools.Add(typeKey, statePool);
                entityPools.Add(typeKey, entityPool);
                entityTypeToKey.Add(entityType, typeKey);
            }
        }

        #region Allocation

        public RailCommand CreateCommand()
        {
            return commandPool.Allocate();
        }

        public RailEntity CreateEntity(int factoryType)
        {
            return entityPools[factoryType].Allocate();
        }

        public RailState CreateState(int factoryType)
        {
            return statePools[factoryType].Allocate();
        }

        public RailEvent CreateEvent(int factoryType)
        {
            return eventPools[factoryType].Allocate();
        }

        public RailStateDelta CreateDelta()
        {
            return deltaPool.Allocate();
        }

        public RailCommandUpdate CreateCommandUpdate()
        {
            return commandUpdatePool.Allocate();
        }

        [OnlyIn(Component.Server)]
        public RailStateRecord CreateRecord()
        {
            return recordPool?.Allocate();
        }

        #region Typed

        public int GetEntityFactoryType<T>()
            where T : RailEntity
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