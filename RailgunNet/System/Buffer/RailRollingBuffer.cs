using System;
using System.Collections.Generic;

namespace RailgunNet.System.Buffer
{
    /// <summary>
    ///     A rolling buffer that contains a sliding window of the most recent
    ///     stored values.
    /// </summary>
    public class RailRollingBuffer<T>
    {
        private readonly int capacity;

        private readonly T[] data;

        private int start;

        public RailRollingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            this.capacity = capacity;

            data = new T[capacity];
            Count = 0;
            start = 0;
        }

        public int Count { get; private set; }

        public void Clear()
        {
            Count = 0;
            start = 0;
        }

        /// <summary>
        ///     Stores a value as latest.
        /// </summary>
        public void Store(T value)
        {
            if (Count < capacity)
            {
                data[Count++] = value;
                IncrementStart();
            }
            else
            {
                data[start] = value;
                IncrementStart();
            }
        }

        /// <summary>
        ///     Returns all values, but not in order.
        /// </summary>
        public IEnumerable<T> GetValues()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return data[i];
            }
        }

        private void IncrementStart()
        {
            start = (start + 1) % capacity;
        }
    }
}
