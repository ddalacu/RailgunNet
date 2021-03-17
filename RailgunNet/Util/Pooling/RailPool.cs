using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Util.Debug;

namespace RailgunNet.Util.Pooling
{
    public static class RailPool
    {
        public static void Free<T>(T instance)
            where T : IRailPoolable<T>
        {
            instance.OwnerPool.Deallocate(instance);
        }

        public static void SafeReplace<T>(ref T destination, T instance)
            where T : IRailPoolable<T>
        {
            if (destination != null) Free(destination);
            destination = instance;
        }

        public static void DrainQueue<T>(Queue<T> queue)
            where T : IRailPoolable<T>
        {
            while (queue.Count > 0)
            {
                Free(queue.Dequeue());
            }
        }
    }

    public interface IRailMemoryPool<T>
    {
        T Allocate();
        void Deallocate(T instance);
    }

    public class RailMemoryPool<T> : IRailMemoryPool<T>
        where T : IRailPoolable<T>
    {
        private readonly IRailFactory<T> factory;
        private readonly Stack<T> freeList;

        public RailMemoryPool(IRailFactory<T> factory)
        {
            this.factory = factory;
            freeList = new Stack<T>();
        }

        public T Allocate()
        {
            if (freeList.Count > 0)
            {
                T obj = freeList.Pop();
                obj.OwnerPool = this;
                obj.Reset();
                return obj;
            }
            else
            {
                T obj = factory.Create();
                obj.OwnerPool = this;
                obj.Allocated();
                return obj;
            }
        }

        public void Deallocate(T instance)
        {
            RailDebug.Assert(instance.OwnerPool == this);

            instance.Reset();
            instance.OwnerPool = null; // Prevent multiple frees
            freeList.Push(instance);
        }
    }
}
