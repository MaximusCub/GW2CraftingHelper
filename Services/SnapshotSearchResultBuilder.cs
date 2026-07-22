using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Blish-free search/filter/aggregation logic for the Snapshot tab's
    /// account-inventory browser (M39 snapshot search,
    /// d1-snapshot-about-settings.md Feature 1). MainView.cs is the only
    /// caller; every method here is a pure function over already-loaded
    /// AccountSnapshot/AccountItemIndex data - no I/O, no Blish_HUD/
    /// Gw2Sharp/Microsoft.Xna usings (repo invariant: tests must stay
    /// Blish-free).
    /// <para>
    /// Row grouping reuses AccountItemIndex/GetPrioritizedSources verbatim
    /// (already covered by AccountItemIndexTests) rather than
    /// re-implementing per-source totals - this class only adds the
    /// search-substring match, the source-category filter, and the
    /// display-row shape on top of it.
    /// </para>
    /// </summary>
    public static class SnapshotSearchResultBuilder
    {
        /// <summary>
        /// Builds one representative <see cref="SnapshotItemEntry"/> per
        /// distinct itemId in <paramref name="items"/> (name/icon are
        /// resolved identically for every entry sharing an itemId - see
        /// Gw2AccountSnapshotService.ResolveItemDetailsAsync's shared
        /// per-id cache - so the first one seen is sufficient). Callers
        /// (MainView) build this once per snapshot, alongside their
        /// AccountItemIndex, and reuse the same map across every
        /// <see cref="BuildItemRows"/> call for that snapshot (e.g. once
        /// per search-box keystroke) instead of re-scanning the full raw
        /// entry list - potentially thousands of rows across a large
        /// account's characters/bank/material storage/shared inventory -
        /// on every call. Returns an empty dictionary, never null, for a
        /// null <paramref name="items"/>; null entries within it are
        /// skipped.
        /// </summary>
        public static Dictionary<int, SnapshotItemEntry> BuildRepresentativeIndex(IReadOnlyList<SnapshotItemEntry> items)
        {
            var firstSeenByItemId = new Dictionary<int, SnapshotItemEntry>();

            if (items == null)
            {
                return firstSeenByItemId;
            }

            foreach (var entry in items)
            {
                if (entry == null)
                {
                    continue;
                }

                if (!firstSeenByItemId.ContainsKey(entry.ItemId))
                {
                    firstSeenByItemId[entry.ItemId] = entry;
                }
            }

            return firstSeenByItemId;
        }

        /// <summary>
        /// Builds one <see cref="SnapshotSearchRow"/> per distinct itemId in
        /// <paramref name="itemsById"/> that (a) matches
        /// <paramref name="searchText"/> by case-insensitive substring
        /// against the item's own name (never against source/character
        /// labels - Feature 1 Open Question 2's accepted choice) and (b)
        /// has a positive total once <paramref name="sourceFilter"/> has
        /// excluded any unchecked sources. An item with zero quantity
        /// across the checked sources drops out of the list entirely
        /// rather than appearing as a zero-count row. Rows are sorted by
        /// name (ordinal, case-insensitive) for a stable, predictable
        /// order across rebuilds. Returns an empty list, never null, for
        /// null/empty <paramref name="itemsById"/> or a null
        /// <paramref name="index"/>.
        /// <para>
        /// <paramref name="itemsById"/> is the already-deduped itemId -&gt;
        /// representative-entry map (see <see cref="BuildRepresentativeIndex"/>)
        /// - this method never re-scans the raw per-source entry list
        /// itself, so it stays cheap to call on every keystroke as long as
        /// the caller builds the map once per snapshot rather than once
        /// per call.
        /// </para>
        /// </summary>
        public static List<SnapshotSearchRow> BuildItemRows(
            IReadOnlyDictionary<int, SnapshotItemEntry> itemsById,
            AccountItemIndex index,
            string searchText,
            SnapshotSourceFilter sourceFilter,
            string activeCharacterName)
        {
            var rows = new List<SnapshotSearchRow>();

            if (itemsById == null || index == null)
            {
                return rows;
            }

            string trimmedSearch = (searchText ?? string.Empty).Trim();

            foreach (var kvp in itemsById)
            {
                int itemId = kvp.Key;

                // Never display raw item IDs (repo invariant).
                string name = string.IsNullOrWhiteSpace(kvp.Value.Name) ? "Unknown Item" : kvp.Value.Name;

                if (trimmedSearch.Length > 0 &&
                    name.IndexOf(trimmedSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var prioritizedSources = AccountItemIndex.GetPrioritizedSources(itemId, index, activeCharacterName);
                var breakdown = new List<SnapshotSourceCount>();
                int total = 0;

                foreach (var source in prioritizedSources)
                {
                    if (!IsSourceEnabled(source, sourceFilter))
                    {
                        continue;
                    }

                    int quantity = index.GetQuantity(itemId, source);
                    if (quantity <= 0)
                    {
                        continue;
                    }

                    breakdown.Add(new SnapshotSourceCount { Label = FormatSourceLabel(source), Count = quantity });
                    total += quantity;
                }

                if (total <= 0)
                {
                    // Every source carrying this item was filtered out (or
                    // the item genuinely has zero quantity everywhere) -
                    // drop the row entirely rather than show a zero total.
                    continue;
                }

                rows.Add(new SnapshotSearchRow
                {
                    ItemId = itemId,
                    Name = name,
                    IconUrl = kvp.Value.IconUrl ?? string.Empty,
                    TotalCount = total,
                    Breakdown = breakdown
                });
            }

            // Secondary key (ItemId) guarantees a fully deterministic order
            // even when two distinct items share the exact same display
            // name - List<T>.Sort is not a stable sort, so without a
            // tiebreaker two same-named items could swap places between
            // otherwise-identical calls (e.g. two rebuilds for the same
            // keystroke) purely due to Dictionary enumeration order, which
            // is not a documented guarantee. Mirrors the same "sorted,
            // deterministic order" bar AccountItemIndex.GetSources already
            // holds itself to (see AccountItemIndexTests.
            // GetSources_ReturnsDeterministicOrder).
            rows.Sort((a, b) =>
            {
                int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : a.ItemId.CompareTo(b.ItemId);
            });
            return rows;
        }

        /// <summary>
        /// Case-insensitive substring filter over wallet entries by
        /// currency name only (source filtering does not apply to
        /// Wallet - currencies have no per-source breakdown at all).
        /// Returns an empty list, never null, for a null
        /// <paramref name="wallet"/>; null entries within it are skipped.
        /// </summary>
        public static List<SnapshotWalletEntry> FilterWallet(IEnumerable<SnapshotWalletEntry> wallet, string searchText)
        {
            var result = new List<SnapshotWalletEntry>();
            if (wallet == null)
            {
                return result;
            }

            string trimmedSearch = (searchText ?? string.Empty).Trim();

            foreach (var entry in wallet)
            {
                if (entry == null)
                {
                    continue;
                }

                string name = entry.CurrencyName ?? string.Empty;
                if (trimmedSearch.Length == 0 ||
                    name.IndexOf(trimmedSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>
        /// True when the raw AccountItemIndex source string passes the
        /// currently-checked categories in <paramref name="filter"/>. A
        /// null filter is treated as "show everything" (matches the
        /// controls' own all-checked default). A raw source string that
        /// matches none of the four known shapes (Bank/MaterialStorage/
        /// SharedInventory/Character:&lt;name&gt;) is shown regardless -
        /// failing open rather than silently hiding real inventory data
        /// the module does not yet recognize (KNOWN-ISSUES #31's "never
        /// silently mask data" posture); there is no such source today.
        /// </summary>
        public static bool IsSourceEnabled(string rawSource, SnapshotSourceFilter filter)
        {
            if (filter == null)
            {
                return true;
            }

            if (string.IsNullOrEmpty(rawSource))
            {
                return false;
            }

            if (rawSource.StartsWith(AccountItemIndex.CharacterSourcePrefix, StringComparison.Ordinal))
            {
                return filter.Characters;
            }

            switch (rawSource)
            {
                case AccountItemIndex.SourceBank: return filter.Bank;
                case AccountItemIndex.SourceMaterialStorage: return filter.MaterialStorage;
                case AccountItemIndex.SourceSharedInventory: return filter.SharedInventory;
                default: return true;
            }
        }

        /// <summary>
        /// Display-formats a raw AccountItemIndex source string, stripping
        /// the internal "Character:" encoding prefix and spacing out the
        /// PascalCase storage-location names (e.g. "MaterialStorage" -&gt;
        /// "Material Storage") - a small polish fix so the raw internal
        /// token never reaches the UI verbatim (d1-snapshot-about-
        /// settings.md Feature 1's explicit call-out; the underlying
        /// strings are already display-safe, not raw ids, so this is
        /// cosmetic only, not a repo-invariant fix). Returns "Unknown" for
        /// a null/empty source.
        /// </summary>
        public static string FormatSourceLabel(string rawSource)
        {
            if (string.IsNullOrEmpty(rawSource))
            {
                return "Unknown";
            }

            if (rawSource.StartsWith(AccountItemIndex.CharacterSourcePrefix, StringComparison.Ordinal))
            {
                return "Character: " + rawSource.Substring(AccountItemIndex.CharacterSourcePrefix.Length);
            }

            switch (rawSource)
            {
                case AccountItemIndex.SourceMaterialStorage: return "Material Storage";
                case AccountItemIndex.SourceSharedInventory: return "Shared Inventory";
                case AccountItemIndex.SourceBank: return "Bank";
                default: return rawSource;
            }
        }
    }
}
