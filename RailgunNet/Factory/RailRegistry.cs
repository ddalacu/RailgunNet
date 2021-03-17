using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using RailgunNet.Logic;

namespace RailgunNet.Factory
{
    public class RailRegistry
    {
        protected List<EntityConstructionInfo> entityTypes { get; }
        protected List<EventConstructionInfo> eventTypes { get; }

        public RailRegistry()
        {
            CommandType = null;
            eventTypes = new List<EventConstructionInfo>();
            entityTypes = new List<EntityConstructionInfo>();
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in loadedAssemblies)
            {
                RailSynchronizedFactory.Detect(assembly);
            }
        }

        public Type CommandType { get; private set; }

        public IEnumerable<EventConstructionInfo> EventTypes => eventTypes;

        public IEnumerable<EntityConstructionInfo> EntityTypes => entityTypes;

        [PublicAPI]
        public void SetCommandType<TCommand>()
            where TCommand : RailCommand
        {
            CommandType = typeof(TCommand);
        }

        [PublicAPI]
        public void AddEventType<TEvent>(object[] constructorParams = null)
            where TEvent : RailEvent
        {
            Type eventType = typeof(TEvent);
            if (!CanBeConstructedWith<TEvent>(constructorParams))
            {
                throw new ArgumentException(
                    $"The provided constructor arguments {constructorParams} do not match any constructors in {eventType}.");
            }

            eventTypes.Add(new EventConstructionInfo(eventType, constructorParams));
        }

        protected static bool CanBeConstructedWith<T>(object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return typeof(T).GetConstructor(Type.EmptyTypes) != null;
            }

            Type[] paramPack = parameters.Select(obj => obj.GetType()).ToArray();
            return typeof(T).GetConstructor(paramPack) != null;
        }
    }

    public class RailRegistry<TEntityBase> : RailRegistry where TEntityBase : IEntity
    {
        /// <summary>
        ///     Adds an entity type with its corresponding state to the registry.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TState"></typeparam>
        /// <param name="paramsEntity">Array of parameters for the entity constructor to invoke or null.</param>
        [PublicAPI]
        public void AddEntityType<TEntity, TState>(object[] paramsEntity = null)
            where TEntity : TEntityBase
            where TState : RailState
        {


            if (!CanBeConstructedWith<TEntity>(paramsEntity))
            {
                throw new ArgumentException(
                    $"The provided constructor arguments {paramsEntity} do not match any constructors in {typeof(TEntity)}.");
            }

            entityTypes.Add(new EntityConstructionInfo(typeof(TEntity), typeof(TState), paramsEntity));
        }
    }
}
