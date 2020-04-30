using System;

namespace RailgunNet.Factory
{
    public class EntityConstructionInfo
    {
        public readonly object[] ConstructorParamsEntity;
        public readonly object[] ConstructorParamsState;

        public EntityConstructionInfo(
            Type entity,
            Type state,
            object[] constructorParamsEntity,
            object[] constructorParamsState)
        {
            Entity = entity;
            State = state;
            ConstructorParamsEntity = constructorParamsEntity;
            ConstructorParamsState = constructorParamsState;
        }

        public Type Entity { get; }
        public Type State { get; }
    }
}
