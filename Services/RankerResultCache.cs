using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The Crafting Ranker's two answer sets - one per comparison mode -
    /// and the rules for when each stops being true.
    ///
    /// <para>
    /// WHY TWO. The two modes answer different questions about the same
    /// rows, and a row's answer under one says nothing about its answer
    /// under the other. Keeping only the last mode's set made every toggle
    /// a full recompute, including a toggle straight back to numbers the
    /// session had already paid for (owner ruling, 2026-08-27).
    /// </para>
    ///
    /// <para>
    /// INVALIDATION, once, here:
    /// </para>
    /// <list type="bullet">
    /// <item><description>CASCADE is order-dependent. A row is measured
    /// after every row above it has claimed materials, currencies, coin and
    /// daily crafts, so ANY change at priority index i - a reorder, an
    /// insert, a removal, a quantity edit - invalidates i and everything
    /// below it. Rows above are untouched: the cascade never reads
    /// downward.</description></item>
    /// <item><description>INDEPENDENT is order-free. Every row is measured
    /// against the whole account, ignoring the others, so reordering
    /// invalidates nothing at all and adding or removing a row invalidates
    /// only that row. A quantity edit invalidates the row it edits, in both
    /// sets.</description></item>
    /// <item><description>A NEW ACCOUNT SNAPSHOT invalidates both sets
    /// whole: every number in either was measured against the holdings the
    /// snapshot replaced.</description></item>
    /// <item><description>A REFRESH of a mode recomputes that mode's set
    /// and leaves the other alone.</description></item>
    /// </list>
    ///
    /// <para>
    /// BOUND: at most RankerWatchlistLimits.MaxEntries rows in each of two
    /// sets. Nothing here grows with time, only with the list the user can
    /// see, and a removed row is dropped from both sets rather than left to
    /// accumulate.
    /// </para>
    /// </summary>
    internal sealed class RankerResultCache
    {
        private readonly Dictionary<RankerMode, Dictionary<int, Entry>> _byMode =
            new Dictionary<RankerMode, Dictionary<int, Entry>>
            {
                [RankerMode.Cascade] = new Dictionary<int, Entry>(),
                [RankerMode.Independent] = new Dictionary<int, Entry>(),
            };

        private sealed class Entry
        {
            internal RankerRowMetrics Metrics;
            internal CraftingPlanResult Owned;
        }

        internal RankerRowMetrics Metrics(RankerMode mode, int itemId)
        {
            return _byMode[mode].TryGetValue(itemId, out var entry) ? entry.Metrics : null;
        }

        /// <summary>
        /// The solve the cached metrics came from, or null. Cascade replays
        /// these instead of re-solving the rows above the first stale one.
        /// </summary>
        internal CraftingPlanResult Owned(RankerMode mode, int itemId)
        {
            return _byMode[mode].TryGetValue(itemId, out var entry) ? entry.Owned : null;
        }

        internal IReadOnlyDictionary<int, CraftingPlanResult> OwnedResults(RankerMode mode)
        {
            var results = new Dictionary<int, CraftingPlanResult>();
            foreach (var pair in _byMode[mode])
            {
                if (pair.Value.Owned != null)
                {
                    results[pair.Key] = pair.Value.Owned;
                }
            }

            return results;
        }

        /// <summary>
        /// The mode's solves, for the metadata lookups a row's sub-lines do
        /// on every render - allocation-free, unlike OwnedResults.
        /// </summary>
        internal IEnumerable<CraftingPlanResult> EnumerateOwned(RankerMode mode)
        {
            foreach (var entry in _byMode[mode].Values)
            {
                if (entry.Owned != null)
                {
                    yield return entry.Owned;
                }
            }
        }

        internal void Store(RankerMode mode, int itemId, RankerRowMetrics metrics, CraftingPlanResult owned)
        {
            _byMode[mode][itemId] = new Entry { Metrics = metrics, Owned = owned };
        }

        /// <summary>True when either set holds anything at all.</summary>
        internal bool HasAnyResults
        {
            get
            {
                foreach (var set in _byMode.Values)
                {
                    if (set.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>A new account snapshot: nothing measured against the old one survives.</summary>
        internal void InvalidateEverything()
        {
            foreach (var set in _byMode.Values)
            {
                set.Clear();
            }
        }

        internal void InvalidateMode(RankerMode mode)
        {
            _byMode[mode].Clear();
        }

        /// <summary>A row's own inputs changed (its quantity): both sets lose it.</summary>
        internal void InvalidateItem(int itemId)
        {
            foreach (var set in _byMode.Values)
            {
                set.Remove(itemId);
            }
        }

        /// <summary>
        /// A change at <paramref name="index"/> in the priority list. Applies
        /// each mode's own rule, so callers never have to remember which one
        /// order matters to.
        /// </summary>
        internal void InvalidateCascadeFrom(IReadOnlyList<RankerWatchlistEntry> entries, int index)
        {
            if (entries == null || index < 0)
            {
                return;
            }

            var cascade = _byMode[RankerMode.Cascade];
            for (int i = index; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    cascade.Remove(entries[i].ItemId);
                }
            }
        }

        /// <summary>
        /// Rows no longer on the list are dropped from both sets - a
        /// removed-then-re-added item must be re-measured, and nothing is
        /// kept alive for a row the user cannot see.
        /// </summary>
        internal void KeepOnly(IReadOnlyList<RankerWatchlistEntry> entries)
        {
            var live = new HashSet<int>();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry != null)
                    {
                        live.Add(entry.ItemId);
                    }
                }
            }

            foreach (var set in _byMode.Values)
            {
                var drop = new List<int>();
                foreach (int itemId in set.Keys)
                {
                    if (!live.Contains(itemId))
                    {
                        drop.Add(itemId);
                    }
                }

                foreach (int itemId in drop)
                {
                    set.Remove(itemId);
                }
            }
        }

        /// <summary>
        /// True when every row on the list already has a usable answer under
        /// this mode - the test for "toggling here needs no solves at all".
        /// Cascade additionally requires each row's answer to have been
        /// measured in the slot it now occupies.
        /// </summary>
        internal bool IsComplete(RankerMode mode, IReadOnlyList<RankerWatchlistEntry> entries)
        {
            return FirstStaleIndex(mode, entries) < 0;
        }

        /// <summary>
        /// The first row a refresh of this mode would have to solve, or -1
        /// when there is none.
        /// <para>
        /// For Cascade this is a PREFIX answer and deliberately so: the
        /// invalidation rules above guarantee that a stale row stales
        /// everything below it, and a run has to walk the rows above the
        /// first stale one anyway to rebuild the availability they consume.
        /// </para>
        /// </summary>
        internal int FirstStaleIndex(RankerMode mode, IReadOnlyList<RankerWatchlistEntry> entries)
        {
            if (entries == null)
            {
                return -1;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                var metrics = Metrics(mode, entry.ItemId);
                if (!RankerPriorityOrdering.MetricsAreCurrent(metrics, i, mode))
                {
                    return i;
                }

                if (mode == RankerMode.Cascade && Owned(mode, entry.ItemId) == null)
                {
                    // Without the solve behind it the cascade cannot replay
                    // this row's claim on the account, so it has to be
                    // re-solved even though its numbers still read true.
                    return i;
                }
            }

            return -1;
        }
    }
}
