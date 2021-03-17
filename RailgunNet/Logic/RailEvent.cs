using RailgunNet.Factory;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic
{

    /// <summary>
    ///     Events are sent attached to entities and represent temporary changes
    ///     in status. They can be sent to specific controllers or broadcast to all
    ///     controllers for whom the entity is in scope.
    /// </summary>
    public abstract class RailEvent : IRailPoolable<RailEvent>
    {
        private readonly RailEventDataSerializer _dataSerializer;

        public EventType FactoryType { get; set; }

        // Synchronized
        public SequenceId EventId { get; set; }

        public EntityId TargetEntity { get; set; }

        // Local only
        public ushort Attempts { get; set; }

        protected RailEvent()
        {
            _dataSerializer = new RailEventDataSerializer(this);
        }

        public RailEvent Clone(RailResource resource)
        {
            RailEvent clone = resource.CreateEvent(FactoryType);
            clone.EventId = EventId;
            clone.Attempts = Attempts;
            clone._dataSerializer.SetDataFrom(_dataSerializer);
            return clone;
        }

        public void RegisterSent()
        {
            if (Attempts > 0) Attempts--;
        }

        public void RegisterSkip()
        {
            RegisterSent();
        }

        #region Pooling

        public IRailMemoryPool<RailEvent> OwnerPool { get; set; }

        void IRailPoolable<RailEvent>.Reset()
        {
            EventId = SequenceId.Invalid;
            Attempts = 0;
            _dataSerializer.ResetData();
        }

        void IRailPoolable<RailEvent>.Allocated()
        {

        }
        #endregion

        public void Encode(RailIntCompressor compressor, RailBitBuffer buffer)
        {
            // Write: [EventType]
            buffer.WriteInt(compressor, (int)FactoryType);

            // Write: [EventId]
            buffer.WriteSequenceId(EventId);

            // Write: [TargetEntity]
            buffer.WriteEntityId(TargetEntity);

            // Write: [EventData]
            _dataSerializer.WriteData(buffer);
        }

        public static RailEvent Decode(
            IRailEventConstruction eventCreator,
            RailIntCompressor compressor,
            RailBitBuffer buffer)
        {
            // Read: [EventType]
            var factoryType = (EventType)buffer.ReadInt(compressor);

            var evnt = eventCreator.CreateEvent(factoryType);

            // Read: [EventId]
            evnt.EventId = buffer.ReadSequenceId();

            // Read: [TargetEntity]
            evnt.TargetEntity = buffer.ReadEntityId();

            // Read: [EventData]
            evnt._dataSerializer.ReadData(buffer);

            return evnt;
        }
    }
}
