using System;

namespace RailgunNet.Factory
{
    public interface IRailFactory<out T>
    {
        T Create();
    }

    public class RailFactory<T> : IRailFactory<T>
    {
        protected readonly Type typeToCreate;

        public RailFactory()
        {
            typeToCreate = typeof(T);
            if (typeToCreate.IsAbstract) throw new ArgumentException("Cannot create a factory for an abstract type.");
        }

        public RailFactory(Type typeToCreate)
        {
            if (!typeToCreate.IsSubclassOf(typeof(T)))
                throw new ArgumentException("type is not derived from base.", nameof(typeToCreate));
            if (typeToCreate.IsAbstract) throw new ArgumentException("Cannot create a factory for an abstract type.");
            this.typeToCreate = typeToCreate;
        }

        public virtual T Create()
        {
            return (T) Activator.CreateInstance(typeToCreate);
        }
    }
}