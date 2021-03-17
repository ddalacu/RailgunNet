using RailgunNet.Factory;
using RailgunNet.System.Buffer;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic
{
    /// <summary>
    ///     Commands contain input data from the client to be applied to entities.
    /// </summary>
    public abstract class RailCommand : IRailPoolable<RailCommand>, IRailTimedValue
    {
        /// <summary>
        ///     The client's local tick (not server predicted) at the time of sending.
        /// </summary>
        public Tick ClientTick { get; set; } // Synchronized

        public bool IsNewCommand { get; set; }

        private readonly RailCommandDataSerializer _dataSerializer;

        Tick IRailTimedValue.Tick => ClientTick;

        protected RailCommand()
        {
            _dataSerializer = new RailCommandDataSerializer(this);
        }

        #region Implementation: IRailPoolable
        IRailMemoryPool<RailCommand> IRailPoolable<RailCommand>.OwnerPool { get; set; }

        void IRailPoolable<RailCommand>.Reset()
        {
            Reset();
        }

        void IRailPoolable<RailCommand>.Allocated()
        {

        }
        #endregion

        private void Reset()
        {
            ClientTick = Tick.INVALID;
            _dataSerializer.ResetData();
        }

        public void Encode(RailBitBuffer buffer)
        {
            // Write: [SenderTick]
            buffer.WriteTick(ClientTick);

            // Write: [Command Data]
            _dataSerializer.EncodeData(buffer);
        }

        public void Decode(RailBitBuffer buffer)
        {
            // Read: [SenderTick]
            ClientTick = buffer.ReadTick();

            // Read: [Command Data]
            _dataSerializer.DecodeData(buffer);
        }
    }
}
