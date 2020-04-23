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
using System.Reflection;
using JetBrains.Annotations;

namespace Railgun
{
    public class RailResource
    {
        public RailIntCompressor EventTypeCompressor { get; }
        public RailIntCompressor EntityTypeCompressor { get; }

        private readonly Dictionary<Type, int> entityTypeToKey;
        private readonly Dictionary<Type, int> eventTypeToKey;

        private readonly IRailMemoryPool<RailCommand> commandPool;
        private readonly Dictionary<int, IRailMemoryPool<RailEntity>> entityPools;
        private readonly Dictionary<int, IRailMemoryPool<RailState>> statePools;
        private readonly Dictionary<int, IRailMemoryPool<RailEvent>> eventPools;

        private readonly IRailMemoryPool<RailStateDelta> deltaPool;
        private readonly IRailMemoryPool<RailCommandUpdate> commandUpdatePool;

        [OnlyIn(Component.Server)]
        [CanBeNull]
        private readonly IRailMemoryPool<RailStateRecord> recordPool = null;

        public RailResource(RailRegistry registry)
        {
            this.entityTypeToKey = new Dictionary<Type, int>();
            this.eventTypeToKey = new Dictionary<Type, int>();

            this.commandPool = this.CreateCommandPool(registry);
            this.entityPools = new Dictionary<int, IRailMemoryPool<RailEntity>>();
            this.statePools = new Dictionary<int, IRailMemoryPool<RailState>>();
            this.eventPools = new Dictionary<int, IRailMemoryPool<RailEvent>>();

            this.RegisterEvents(registry);
            this.RegisterEntities(registry);

            this.EventTypeCompressor =
              new RailIntCompressor(0, this.eventPools.Count + 1);
            this.EntityTypeCompressor =
              new RailIntCompressor(0, this.entityPools.Count + 1);

            this.deltaPool = new RailMemoryPool<RailStateDelta>(new RailFactory<RailStateDelta>());
            this.commandUpdatePool = new RailMemoryPool<RailCommandUpdate>(new RailFactory<RailCommandUpdate>());

            if (registry.Component == Component.Server)
            {
                this.recordPool = new RailMemoryPool<RailStateRecord>(new RailFactory<RailStateRecord>());
            }
        }

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

                int typeKey = this.eventPools.Count + 1; // 0 is an invalid type
                this.eventPools.Add(typeKey, statePool);
                this.eventTypeToKey.Add(eventType, typeKey);
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

                int typeKey = this.statePools.Count + 1; // 0 is an invalid type
                this.statePools.Add(typeKey, statePool);
                this.entityPools.Add(typeKey, entityPool);
                this.entityTypeToKey.Add(entityType, typeKey);
            }
        }

        #region Allocation
        public RailCommand CreateCommand()
        {
            return this.commandPool.Allocate();
        }

        public RailEntity CreateEntity(int factoryType)
        {
            return this.entityPools[factoryType].Allocate();
        }

        public RailState CreateState(int factoryType)
        {
            return this.statePools[factoryType].Allocate();
        }

        public RailEvent CreateEvent(int factoryType)
        {
            return this.eventPools[factoryType].Allocate();
        }

        public RailStateDelta CreateDelta()
        {
            return this.deltaPool.Allocate();
        }

        public RailCommandUpdate CreateCommandUpdate()
        {
            return this.commandUpdatePool.Allocate();
        }

        [OnlyIn(Component.Server)]
        public RailStateRecord CreateRecord()
        {
            return this.recordPool?.Allocate();
        }
        #region Typed
        public int GetEntityFactoryType<T>()
          where T : RailEntity
        {
            return this.entityTypeToKey[typeof(T)];
        }

        public int GetEventFactoryType<T>()
          where T : RailEvent
        {
            return this.eventTypeToKey[typeof(T)];
        }
        #endregion
        #endregion
    }
}
