using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;

namespace RailgunNet.Connection.Client
{
    /// <summary>
    ///     Packet sent from server to client.
    /// </summary>
    public sealed class RailPacketFromServer
        : RailPacketIncoming
    {
        private readonly RailPackedListS2C<RailStateDelta> deltas;
        Tick ServerTick { get; }

        public RailPacketFromServer()
        {
            deltas = new RailPackedListS2C<RailStateDelta>();
        }

        public IEnumerable<RailStateDelta> Deltas => deltas.Received;

        public override void Reset()
        {
            base.Reset();

            deltas.Clear();
        }

        #region Encode/Decode

        public override void DecodePayload(
            RailResource resource,
            RailBitBuffer buffer)
        {
            // Read: [Deltas]
            DecodeDeltas(resource, buffer);
        }

        private void DecodeDeltas(
            RailResource resource,
            RailBitBuffer buffer)
        {
            deltas.Decode(
                buffer,
                () => RailState.DecodeDelta(resource, buffer, SenderTick));
        }

        #endregion
    }
}