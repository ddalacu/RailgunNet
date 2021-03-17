namespace RailgunNet.Util.Pooling
{
    /// <summary>
    ///     Interface for all types that can be managed by a RailMemoryPool
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IRailPoolable<T> where T : IRailPoolable<T>
    {
        /// <summary>
        ///     The pool that allocated this instance.
        /// </summary>
        IRailMemoryPool<T> OwnerPool { get; set; }

        /// <summary>
        ///     Called exactly once after an instance is allocated. Not called when an instance is reused!
        /// </summary>
        void Allocated();

        void Reset();
    }
}
