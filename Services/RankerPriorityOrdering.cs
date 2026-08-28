using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure list-state transitions for the Crafting Ranker's priority order,
    /// in the shape of ItemRowRequestBuilder rather than a new style of
    /// helper. Mutates in place; the caller persists.
    ///
    /// Because a row's numbers depend on its position (see
    /// RankerPriorityCascade), every mutation also reports the lowest index
    /// whose cached metrics are now invalid - see InvalidatedFromIndex.
    /// </summary>
    public static class RankerPriorityOrdering
    {
        /// <summary>Nothing was invalidated.</summary>
        public const int NoInvalidation = -1;

        /// <summary>
        /// Swaps index with index-1. Returns the lowest index whose metrics
        /// are now stale, or NoInvalidation when nothing moved.
        /// </summary>
        public static int MoveUp(IList<RankerWatchlistEntry> entries, int index)
        {
            if (!CanMoveUp(index, entries?.Count ?? 0))
            {
                return NoInvalidation;
            }

            var moved = entries[index];
            entries[index] = entries[index - 1];
            entries[index - 1] = moved;
            return index - 1;
        }

        /// <summary>Swaps index with index+1. See MoveUp for the return value.</summary>
        public static int MoveDown(IList<RankerWatchlistEntry> entries, int index)
        {
            if (!CanMoveDown(index, entries?.Count ?? 0))
            {
                return NoInvalidation;
            }

            var moved = entries[index];
            entries[index] = entries[index + 1];
            entries[index + 1] = moved;
            return index;
        }

        public static bool CanMoveUp(int index, int count)
        {
            return index > 0 && index < count;
        }

        public static bool CanMoveDown(int index, int count)
        {
            return index >= 0 && index < count - 1;
        }

        /// <summary>Existing index of itemId, or -1. The duplicate-add check.</summary>
        public static int IndexOfItem(IReadOnlyList<RankerWatchlistEntry> entries, int itemId)
        {
            if (entries == null)
            {
                return -1;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].ItemId == itemId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Whether cached metrics may still be displayed for the row at
        /// priorityIndex under the active mode. Two staleness laws in one
        /// place: a mode toggle stales everything computed under the other
        /// mode (both directions - toggling back revives the survivors), and
        /// in Cascade mode a position change stales the row because its
        /// numbers are a function of its slot. Independent metrics are
        /// position-free, so only the mode has to match.
        /// </summary>
        internal static bool MetricsAreCurrent(
            RankerRowMetrics metrics, int priorityIndex, RankerMode mode)
        {
            if (metrics == null || metrics.Mode != mode)
            {
                return false;
            }

            return mode == RankerMode.Independent || metrics.PriorityIndex == priorityIndex;
        }

        /// <summary>
        /// Display order for Independent mode: priority indices sorted so
        /// the row closest to done is first - finished rows, then measured
        /// readiness descending, then not-measurable, then rows with no
        /// current metrics. Ties keep the user's priority order (the sort
        /// is stable), and the entries list itself is never touched - the
        /// hand-set order is what Cascade mode restores.
        /// </summary>
        internal static List<int> IndependentDisplayOrder(
            IReadOnlyList<RankerWatchlistEntry> entries,
            Func<RankerWatchlistEntry, RankerRowMetrics> currentMetricsFor)
        {
            var order = new List<int>();
            if (entries == null)
            {
                return order;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                order.Add(i);
            }

            if (currentMetricsFor == null)
            {
                return order;
            }

            var keys = new double[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                keys[i] = DisplaySortKey(currentMetricsFor(entries[i]));
            }

            // Stable by construction: the key ties break on the priority
            // index itself.
            order.Sort((a, b) =>
            {
                int byKey = keys[b].CompareTo(keys[a]);
                return byKey != 0 ? byKey : a.CompareTo(b);
            });
            return order;
        }

        private static double DisplaySortKey(RankerRowMetrics metrics)
        {
            if (metrics == null)
            {
                // Not yet calculated: below everything that carries data.
                return -1.0;
            }

            switch (metrics.Kind)
            {
                case RankerReadinessKind.NothingLeft:
                    // Finished IS "closest to done".
                    return 2.0;
                case RankerReadinessKind.Measured:
                    return metrics.Readiness;
                default:
                    // Not measurable: something is outstanding but nothing
                    // scoreable - between measured rows and uncalculated ones.
                    return -0.5;
            }
        }

        /// <summary>
        /// Removes the entry at index. Returns the lowest stale index, which
        /// is the removed index itself - every row that shifted up into it
        /// now sits at a different position in the cascade.
        /// </summary>
        public static int RemoveAt(IList<RankerWatchlistEntry> entries, int index)
        {
            if (entries == null || index < 0 || index >= entries.Count)
            {
                return NoInvalidation;
            }

            entries.RemoveAt(index);
            return index;
        }
    }
}
