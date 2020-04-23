using System;

namespace Railgun
{
    public interface IRailFactory<out T>
    {
        T Create();
    }

    public class RailFactory<T> : IRailFactory<T>
    {
        private readonly Type typeToCreate;

        public RailFactory()
        {
            this.typeToCreate = typeof(T);
        }
        public RailFactory(Type typeToCreate)
        {
            if (!typeToCreate.IsSubclassOf(typeof(T)))
            {
                throw new ArgumentException("type is not derived from base.", nameof(typeToCreate));
            }
            this.typeToCreate = typeToCreate;
        }
        public T Create()
        {
            return (T)Activator.CreateInstance(typeToCreate);
        }
    }
}