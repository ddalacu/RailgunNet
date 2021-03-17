using System.Collections.Generic;
using JetBrains.Annotations;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RailgunNet.System;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Connection
{

    public abstract class RailPeer
    {

        protected RailPeer()
        {
            _outgoingEvents = new Queue<RailEvent>();
            lastQueuedEventId = SequenceId.Start.Next;
            _eventHistory = new RailHistory(RailConfig.HISTORY_CHUNKS);
        }

        public Tick LatestRemote { get; private set; }

        /// <summary>
        ///     Allocates a packet and writes common boilerplate information to it.
        /// </summary>
        protected void PrepareSend(RailPacketOutgoing outgoing, Tick localTick)
        {
            outgoing.Initialize(localTick, LatestRemote, _eventHistory.Latest, FilterOutgoingEvents());
        }

        /// <summary>
        ///     Records acknowledging information for the packet.
        /// </summary>
        public virtual void ProcessPacket(Tick localTick, RailPacketIncoming packetBase)
        {
            if (LatestRemote.IsValid == false)
            {
                LatestRemote = packetBase.SenderTick;
            }
            else
            {
                if (packetBase.SenderTick > LatestRemote)
                    LatestRemote = packetBase.SenderTick;
            }

            foreach (var evnt in FilterIncomingEvents(packetBase.Events))
            {
                ProcessEvent(evnt);
            }

            CleanOutgoingEvents(packetBase.LastAckEventId);
        }

        #region Event-Related
        /// <summary>
        ///     Used for uniquely identifying outgoing events.
        /// </summary>
        private SequenceId lastQueuedEventId;

        /// <summary>
        ///     A rolling queue for outgoing reliable events, in order.
        /// </summary>
        private readonly Queue<RailEvent> _outgoingEvents;

        /// <summary>
        ///     A history buffer of received events.
        /// </summary>
        private readonly RailHistory _eventHistory;

        /// <summary>
        ///     Queues an event to send directly to this peer (used internally).
        /// </summary>
        internal void SendEvent([NotNull] RailEvent evnt, ushort attempts)
        {
            // TODO: Event scoping
            evnt.EventId = lastQueuedEventId;
            evnt.Attempts = attempts;

            _outgoingEvents.Enqueue(evnt);
            lastQueuedEventId = lastQueuedEventId.Next;
        }

        /// <summary>
        ///     Removes any acked or expired outgoing events.
        /// </summary>
        private void CleanOutgoingEvents(SequenceId ackedEventId)
        {
            if (ackedEventId.IsValid == false) return;

            // Stop attempting to send acked events
            foreach (RailEvent evnt in _outgoingEvents)
            {
                if (evnt.EventId <= ackedEventId)
                {
                    evnt.Attempts = 0;
                }
            }

            // Clean out any events with zero attempts left
            while (_outgoingEvents.Count > 0)
            {
                if (_outgoingEvents.Peek().Attempts > 0) break;
                RailPool.Free(_outgoingEvents.Dequeue());
            }
        }

        public abstract bool ShouldSendEvent(RailEvent railEvent);

        /// <summary>
        ///     Selects outgoing events to send.
        /// </summary>
        private IEnumerable<RailEvent> FilterOutgoingEvents()
        {
            // The receiving client can only store a limited size sequence history
            // of events in its received buffer, and will skip any events older than
            // its latest received minus that history length, including reliable
            // events. In order to make sure we don't force the client to skip an
            // event with attempts remaining, we will throttle the outgoing events
            // if we've been sending them too fast. For example, if we have a live 
            // event with ID 3 pending, and a maximum history length of 64 (see
            // RailConfig.HISTORY_CHUNKS) then the highest ID we can send would be 
            // ID 67. Were we to send an event with ID 68, then the client may ignore
            // ID 3 when it comes in for being too old, even though it's still live.
            //
            // In practice this shouldn't be a problem unless we're sending way 
            // more events than is reasonable(/possible) in a single packet, or 
            // something is wrong with reliable event acking. You can always increase
            // the number of history chunks if this becomes an issue.

            var firstId = SequenceId.Invalid;
            foreach (var evnt in _outgoingEvents)
            {
                // Ignore dead events, they'll be cleaned up eventually
                if (evnt.Attempts <= 0) continue;

                if (ShouldSendEvent(evnt) == false)
                {
                    evnt.RegisterSkip();
                    continue;
                }

                if (firstId.IsValid == false) firstId = evnt.EventId;
                RailDebug.Assert(firstId <= evnt.EventId);

                if (_eventHistory.AreInRange(firstId, evnt.EventId) == false)
                {
                    RailDebug.LogWarning("Throttling events due to lack of ack");
                    break;
                }

                yield return evnt;
            }
        }

        /// <summary>
        ///     Gets all events that we haven't processed yet, in order with no gaps.
        /// </summary>
        private IEnumerable<RailEvent> FilterIncomingEvents(IEnumerable<RailEvent> events)
        {
            foreach (var evnt in events)
            {
                if (_eventHistory.IsNewId(evnt.EventId))
                {
                    yield return evnt;
                }
            }
        }

        /// <summary>
        ///     Handles the execution of an incoming event.
        /// </summary>
        private void ProcessEvent(RailEvent evnt)
        {
            HandleEventReceived(evnt);
            _eventHistory.Store(evnt.EventId);
        }

        public abstract void HandleEventReceived(RailEvent @event);

        #endregion
    }

}
