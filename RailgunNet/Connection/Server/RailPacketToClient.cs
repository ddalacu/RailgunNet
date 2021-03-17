using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using UnityEngine;


namespace RailgunNet.Connection.Server
{
    /// <summary>
    ///     Packet sent from server to client. Corresponding packet on client
    ///     side is RailPacketFromServer.
    /// </summary>
    public sealed class RailPacketToClient : RailPacketOutgoing
    {
        private readonly RailPackedListOutgoing<RailStateDelta> deltas;

        public double PingProcessDelay { get; set; }

        public RailPacketToClient()
        {
            deltas = new RailPackedListOutgoing<RailStateDelta>();
        }

        public IEnumerable<RailStateDelta> Sent => deltas.Sent;

        public override void Reset()
        {
            base.Reset();

            deltas.Clear();
        }

        public void Populate(
            IEnumerable<RailStateDelta> activeDeltas,
            IEnumerable<RailStateDelta> frozenDeltas,
            IEnumerable<RailStateDelta> removedDeltas)
        {
            deltas.AddPending(removedDeltas);
            deltas.AddPending(frozenDeltas);
            deltas.AddPending(activeDeltas);
        }

        #region Encode/Decode
        public override void EncodePayload(
            IRailStateConstruction stateCreator,
            RailBitBuffer buffer,
            Tick localTick,
            int reservedBytes)
        {
            const double maxValue = 500;
            if (PingProcessDelay < 0)
                PingProcessDelay = 0;
            if (PingProcessDelay > maxValue)
                PingProcessDelay = maxValue;


            var remap = PingProcessDelay / maxValue;
            var ush = (ushort)(remap * ushort.MaxValue);
            buffer.Write(16, ush);

            // Write: [Deltas]
            EncodeDeltas(stateCreator, buffer, reservedBytes);
        }

        private void EncodeDeltas(
            IRailStateConstruction stateCreator,
            RailBitBuffer buffer,
            int reservedBytes)
        {
            deltas.Encode(
                buffer,
                RailConfig.PACKCAP_MESSAGE_TOTAL - reservedBytes,
                RailConfig.MAXSIZE_ENTITY,
                (delta, buf) =>
                {
                    RailStateDeltaSerializer.EncodeDelta(stateCreator, buf, delta);
                });
        }
        #endregion
    }
}
