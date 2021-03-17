using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic
{
    /// <summary>
    ///     States are the fundamental data management class of Railgun. They
    ///     contain all of the synchronized information that an Entity needs to
    ///     function. States have multiple categories that are sent at different
    ///     cadences. In order to synchronize a property, use the appropriate
    ///     Attribute. The following attributes can be used:
    ///     [Immutable]
    ///     Sent only once at creation. Can not be changed after.
    ///     [Mutable]
    ///     Sent whenever the state differs from the client's view.
    ///     Delta-encoded against the client's view.
    ///     [Controller]
    ///     Sent to the controller of the entity every update.
    ///     Not delta-encoded -- always sent full-encode.
    /// </summary>
    public abstract class RailState : IRailPoolable<RailState>
    {
        private const uint FLAGS_ALL = 0xFFFFFFFF; // All values different

        /// <summary>
        /// Tells which mutable fields changed by using one bit for each mutable field
        /// </summary>
        public uint Flags { get; set; } // Synchronized

        public bool HasControllerData { get; set; } // Synchronized

        /// <summary>
        /// Tells if state contains immutable fields datas
        /// </summary>
        public bool HasImmutableData { get; set; } // Synchronized

        public RailStateDataSerializer DataSerializer { get; private set; }

        public RailState()
        {
            DataSerializer = new RailStateDataSerializer(this);
        }

        public RailState Clone(IRailStateConstruction stateCreator)
        {
            var type = stateCreator.GetEntityType(this);
            var clone = stateCreator.CreateState(type);
            clone.OverwriteFrom(this);
            return clone;
        }

        public void OverwriteFrom(RailState source)
        {
            DataSerializer.ApplyImmutableFrom(source.DataSerializer);
            Flags = source.Flags;
            DataSerializer.ApplyMutableFrom(source.DataSerializer, FLAGS_ALL);
            DataSerializer.ApplyControllerFrom(source.DataSerializer);
            HasControllerData = source.HasControllerData;
            HasImmutableData = source.HasImmutableData;
        }

        public void ApplyDelta(RailStateDelta delta)
        {
            RailState deltaState = delta.State;
            HasImmutableData = delta.HasImmutableData || HasImmutableData;
            if (deltaState.HasImmutableData)
            {
                DataSerializer.ApplyImmutableFrom(deltaState.DataSerializer);
            }

            DataSerializer.ApplyMutableFrom(deltaState.DataSerializer, deltaState.Flags);

            DataSerializer.ResetControllerData();
            if (deltaState.HasControllerData)
            {
                DataSerializer.ApplyControllerFrom(deltaState.DataSerializer);
            }

            HasControllerData = delta.HasControllerData;
        }

        #region Pooling
        IRailMemoryPool<RailState> IRailPoolable<RailState>.OwnerPool { get; set; }

        void IRailPoolable<RailState>.Reset()
        {
            Flags = 0;
            HasControllerData = false;
            HasImmutableData = false;
            DataSerializer.ResetAllData();
        }

        void IRailPoolable<RailState>.Allocated()
        {
           
        }
        #endregion
    }
}
