using System.Collections.Generic;
using System.Diagnostics;
using RailgunNet.System.Types;

namespace RailgunNet.System
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct RailViewEntry
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public static readonly RailViewEntry INVALID = new RailViewEntry(Tick.INVALID, true);

        public bool IsValid => lastReceivedTick.IsValid;
        public Tick LastReceivedTick => lastReceivedTick;
        public bool IsFrozen { get; }

        private readonly Tick lastReceivedTick;

        public RailViewEntry(Tick lastReceivedTick, bool isFrozen)
        {
            this.lastReceivedTick = lastReceivedTick;
            IsFrozen = isFrozen;
        }
    }

    public class RailView
    {
        private readonly Dictionary<EntityId, RailViewEntry> latestUpdates;

        private readonly List<KeyValuePair<EntityId, RailViewEntry>> sortList;

        private readonly ViewComparer viewComparer;

        public RailView()
        {
            viewComparer = new ViewComparer();
            latestUpdates = new Dictionary<EntityId, RailViewEntry>();
            sortList = new List<KeyValuePair<EntityId, RailViewEntry>>();
        }

        /// <summary>
        ///     Returns the latest tick the peer has acked for this entity ID.
        /// </summary>
        public RailViewEntry GetLatest(EntityId id)
        {
            if (latestUpdates.TryGetValue(id, out RailViewEntry result)) return result;
            return RailViewEntry.INVALID;
        }

        public void Clear()
        {
            latestUpdates.Clear();
        }

        /// <summary>
        ///     Records an acked status from the peer for a given entity ID.
        /// </summary>
        public void RecordUpdate(
            EntityId entityId,
            Tick receivedTick,
            bool isFrozen)
        {
            RecordUpdate(entityId, new RailViewEntry(receivedTick, isFrozen));
        }

        /// <summary>
        ///     Records an acked status from the peer for a given entity ID.
        /// </summary>
        public void RecordUpdate(EntityId entityId, RailViewEntry entry)
        {
            if (latestUpdates.TryGetValue(entityId, out RailViewEntry currentEntry))
            {
                if (currentEntry.LastReceivedTick > entry.LastReceivedTick)
                {
                    return;
                }
            }

            latestUpdates[entityId] = entry;
        }

        public void CopyFrom(RailView other)
        {
            foreach (KeyValuePair<EntityId, RailViewEntry> pair in other.latestUpdates)
            {
                RecordUpdate(pair.Key, pair.Value);
            }
        }

        /// <summary>
        ///     Views sort in descending tick order. When sending a view to the server
        ///     we send the most recent updated entities since they're the most likely
        ///     to actually matter to the server/client scope.
        /// </summary>
        public IEnumerable<KeyValuePair<EntityId, RailViewEntry>> GetOrdered(Tick localTick)
        {
            sortList.Clear();

            //todo restore this
            //// If we haven't received an update on an entity for too long, don't
            //// bother sending a view for it (the server will update us eventually)
            //foreach (KeyValuePair<EntityId, RailViewEntry> pair in latestUpdates)
            //{
            //    if (localTick - pair.Value.LocalUpdateTick < RailConfig.VIEW_TICKS)
            //    {
            //        sortList.Add(pair);
            //    }
            //}

            sortList.Sort(viewComparer);
            sortList.Reverse();
            return sortList;
        }

        private class ViewComparer : Comparer<KeyValuePair<EntityId, RailViewEntry>>
        {
            public override int Compare(
                KeyValuePair<EntityId, RailViewEntry> x,
                KeyValuePair<EntityId, RailViewEntry> y)
            {
                return Tick.DefaultComparer.Compare(x.Value.LastReceivedTick, y.Value.LastReceivedTick);
            }
        }
    }
}
