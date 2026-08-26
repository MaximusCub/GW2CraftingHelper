using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
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
