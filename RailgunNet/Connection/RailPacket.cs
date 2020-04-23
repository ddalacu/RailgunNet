/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */

using System.Collections.Generic;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Connection
{
    public interface IRailPacket
    {
        void Encode(RailResource resource, RailBitBuffer buffer);
    }

    public abstract class RailPacket
        : IRailPoolable<RailPacket>
            , IRailPacket
    {
        private readonly List<RailEvent> events;

        private readonly List<RailEvent> pendingEvents;

        private int eventsWritten;

        private Tick senderTick;

        protected RailPacket()
        {
            senderTick = Tick.INVALID;
            AckTick = Tick.INVALID;
            AckEventId = SequenceId.INVALID;

            pendingEvents = new List<RailEvent>();
            events = new List<RailEvent>();
            eventsWritten = 0;
        }

        /// <summary>
        ///     The latest tick from the sender.
        /// </summary>
        public Tick SenderTick => senderTick;

        /// <summary>
        ///     The last tick the sender received.
        /// </summary>
        public Tick AckTick { get; private set; }

        /// <summary>
        ///     The last event id the sender received.
        /// </summary>
        public SequenceId AckEventId { get; private set; }

        /// <summary>
        ///     All received events from the sender, in order.
        /// </summary>
        public IEnumerable<RailEvent> Events => events;

        public void Initialize(
            Tick senderTick,
            Tick ackTick,
            SequenceId ackEventId,
            IEnumerable<RailEvent> events)
        {
            this.senderTick = senderTick;
            AckTick = ackTick;
            AckEventId = ackEventId;

            pendingEvents.AddRange(events);
            eventsWritten = 0;
        }

        public virtual void Reset()
        {
            senderTick = Tick.INVALID;
            AckTick = Tick.INVALID;
            AckEventId = SequenceId.INVALID;

            pendingEvents.Clear();
            events.Clear();
            eventsWritten = 0;
        }

        #region Pooling

        IRailMemoryPool<RailPacket> IRailPoolable<RailPacket>.Pool { get; set; }

        void IRailPoolable<RailPacket>.Reset()
        {
            Reset();
        }

        #endregion

        #region Encoding/Decoding

        protected abstract void EncodePayload(
            RailResource resource,
            RailBitBuffer buffer,
            Tick localTick,
            int reservedBytes);

        protected abstract void DecodePayload(
            RailResource resource,
            RailBitBuffer buffer);

        /// <summary>
        ///     After writing the header we write the packet data in three passes.
        ///     The first pass is a fill of events up to a percentage of the packet.
        ///     The second pass is the payload value, which will try to fill the
        ///     remaining packet space. If more space is available, we will try
        ///     to fill it with any remaining events, up to the maximum packet size.
        /// </summary>
        public void Encode(
            RailResource resource,
            RailBitBuffer buffer)
        {
            // Write: [Header]
            EncodeHeader(buffer);

            // Write: [Events] (Early Pack)
            EncodeEvents(resource, buffer, RailConfig.PACKCAP_EARLY_EVENTS);

            // Write: [Payload] (+1 byte for the event count)
            EncodePayload(resource, buffer, senderTick, 1);

            // Write: [Events] (Fill Pack)
            EncodeEvents(resource, buffer, RailConfig.PACKCAP_MESSAGE_TOTAL);
        }

        public void Decode(
            RailResource resource,
            RailBitBuffer buffer)
        {
            // Read: [Header]
            DecodeHeader(buffer);

            // Read: [Events] (Early Pack)
            DecodeEvents(resource, buffer);

            // Read: [Payload]
            DecodePayload(resource, buffer);

            // Read: [Events] (Fill Pack)
            DecodeEvents(resource, buffer);
        }

        #region Header

        private void EncodeHeader(RailBitBuffer buffer)
        {
            RailDebug.Assert(senderTick.IsValid);

            // Write: [LocalTick]
            buffer.WriteTick(senderTick);

            // Write: [AckTick]
            buffer.WriteTick(AckTick);

            // Write: [AckReliableEventId]
            buffer.WriteSequenceId(AckEventId);
        }

        private void DecodeHeader(RailBitBuffer buffer)
        {
            // Read: [LocalTick]
            senderTick = buffer.ReadTick();

            // Read: [AckTick]
            AckTick = buffer.ReadTick();

            // Read: [AckReliableEventId]
            AckEventId = buffer.ReadSequenceId();
        }

        #endregion

        #region Events

        /// <summary>
        ///     Writes as many events as possible up to maxSize and returns the number
        ///     of events written in the batch. Also increments the total counter.
        /// </summary>
        private void EncodeEvents(
            RailResource resource,
            RailBitBuffer buffer,
            int maxSize)
        {
            eventsWritten +=
                buffer.PackToSize(
                    maxSize,
                    RailConfig.MAXSIZE_EVENT,
                    GetNextEvents(),
                    evnt => evnt.Encode(resource, buffer, senderTick),
                    evnt => evnt.RegisterSent());
        }

        private void DecodeEvents(
            RailResource resource,
            RailBitBuffer buffer)
        {
            IEnumerable<RailEvent> decoded =
                buffer.UnpackAll(
                    () => RailEvent.Decode(resource, buffer, SenderTick));
            foreach (RailEvent evnt in decoded)
                events.Add(evnt);
        }

        private IEnumerable<RailEvent> GetNextEvents()
        {
            for (int i = eventsWritten; i < pendingEvents.Count; i++)
                yield return pendingEvents[i];
        }

        #endregion

        #endregion
    }
}