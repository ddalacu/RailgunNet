﻿using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;

namespace RailgunNet.Connection
{
    public static class RailPacketSerializer
    {
        /// <summary>
        ///     After writing the header we write the packet data in three passes.
        ///     The first pass is a fill of events up to a percentage of the packet.
        ///     The second pass is the payload value, which will try to fill the
        ///     remaining packet space. If more space is available, we will try
        ///     to fill it with any remaining events, up to the maximum packet size.
        /// </summary>
        public static void Encode(
            RailPacketOutgoing packet,
            RailResource resource,
            RailBitBuffer buffer)
        {
            // Write: [Header]
            EncodeHeader(packet, buffer);

            // Write: [Events] (Early Pack)
            EncodeEvents(packet, resource.EventTypeCompressor, buffer, RailConfig.PACKCAP_EARLY_EVENTS);

            // Write: [Payload] (+1 byte for the event count)
            packet.EncodePayload(resource, buffer, packet.SenderTick, 1);

            // Write: [Events] (Fill Pack)
            EncodeEvents(packet, resource.EventTypeCompressor, buffer, RailConfig.PACKCAP_MESSAGE_TOTAL);
        }

        public static void Decode(
            RailPacketIncoming packet,
            RailResource resource,
            RailBitBuffer buffer)
        {
            // Read: [Header]
            DecodeHeader(packet, buffer);

            // Read: [Events] (Early Pack)
            DecodeEvents(packet, resource, resource.EventTypeCompressor, buffer);

            // Read: [Payload]
            packet.DecodePayload(resource, buffer);

            // Read: [Events] (Fill Pack)
            DecodeEvents(packet, resource, resource.EventTypeCompressor, buffer);
        }

        #region Header

        private static void EncodeHeader(
            RailPacketOutgoing packet,
            RailBitBuffer buffer)
        {
            RailDebug.Assert(packet.SenderTick.IsValid);

            // Write: [LocalTick]
            buffer.WriteTick(packet.SenderTick);

            // Write: [AckTick]
            buffer.WriteTick(packet.AckTick);

            // Write: [AckReliableEventId]
            buffer.WriteSequenceId(packet.AckEventId);
        }

        private static void DecodeHeader(
            RailPacketIncoming packet,
            RailBitBuffer buffer)
        {
            // Read: [LocalTick]
            packet.SenderTick = buffer.ReadTick();

            // Read: [AckTick]
            packet.AckTick = buffer.ReadTick();

            // Read: [AckReliableEventId]
            packet.AckEventId = buffer.ReadSequenceId();
        }

        #endregion

        #region Events

        /// <summary>
        ///     Writes as many events as possible up to maxSize and returns the number
        ///     of events written in the batch. Also increments the total counter.
        /// </summary>
        private static void EncodeEvents(
            RailPacketOutgoing packet,
            RailIntCompressor compressor,
            RailBitBuffer buffer,
            int maxSize)
        {
            packet.EventsWritten +=
                buffer.PackToSize(
                    maxSize,
                    RailConfig.MAXSIZE_EVENT,
                    packet.GetNextEvents(),
                    evnt => evnt.Encode(compressor, buffer, packet.SenderTick),
                    evnt => evnt.RegisterSent());
        }

        private static void DecodeEvents(
            RailPacketIncoming packet,
            IRailEventCreator creator,
            RailIntCompressor compressor,
            RailBitBuffer buffer)
        {
            IEnumerable<RailEvent> decoded =
                buffer.UnpackAll(
                    () => RailEvent.Decode(creator, compressor, buffer, packet.SenderTick));
            foreach (RailEvent evnt in decoded)
                packet.Events.Add(evnt);
        }

        #endregion
    }
}