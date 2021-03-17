using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Encoding;
using UnityEngine;

namespace RailgunNet.Connection.Client
{
    /// <summary>
    ///     Packet sent from server to client.
    /// </summary>
    public sealed class RailPacketFromServer : RailPacketIncoming
    {
        private readonly RailPackedListIncoming<RailStateDelta> deltas;

        public double PingProcessDelay { get; set; }

        public RailPacketFromServer()
        {
            deltas = new RailPackedListIncoming<RailStateDelta>();
        }

        public IEnumerable<RailStateDelta> Deltas => deltas.Received;

        public override void Reset()
        {
            base.Reset();

            deltas.Clear();
        }

        #region Encode/Decode
        public override void DecodePayload(
            IRailCommandConstruction commandCreator,
            IRailStateConstruction stateCreator,
            RailBitBuffer buffer)
        {
            const double maxValue = 500d;
            var encoded= (ushort)buffer.Read(16);
            var map = ((double)encoded / ushort.MaxValue);
            PingProcessDelay = (map * maxValue);

            // Read: [Deltas]
            DecodeDeltas(stateCreator, buffer);
        }

        private void DecodeDeltas(IRailStateConstruction stateCreator, RailBitBuffer buffer)
        {
            deltas.Decode(
                buffer,
                buf => RailStateDeltaSerializer.DecodeDelta(stateCreator, buf, SenderTick));
        }
        #endregion
    }
}
