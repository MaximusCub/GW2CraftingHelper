using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Blish-free search/filter/aggregation logic for the Snapshot tab's
    /// account-inventory browser (snapshot search,
    /// dev/proposals/d1-snapshot-about-settings.md Feature 1). MainView.cs is the only
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
    internal static class SnapshotSearchResultBuilder
    {
        /// <summary>
        /// Shortest query allowed to match a character label. Item and
        /// currency names keep matching from the first keystroke; only the
        /// character half is held back, because a single letter surfaces
        /// everything a character whose name contains it holds - so the
        /// opening keystrokes of an item search would widen the list
        /// instead of narrowing it (maintainer decision, char-search-min2).
        /// </summary>
        private const int MinCharacterSearchLength = 2;

        /// <summary>
        /// The extra line the Snapshot tab's "No items match ..." message
        /// carries when <see cref="MinCharacterSearchLength"/> is the reason
        /// the list is empty, and null in every other case.
        /// <para>
        /// The hold-back is deliberate but invisible: a one-letter query
        /// that a character's name does contain looks like a plain
        /// no-results, so the user reads the tab as broken rather than as
        /// waiting for a second letter. The line is emitted ONLY on that
        /// exact case - a query shorter than the minimum, and a roster
        /// character whose name really would match it at the next keystroke
        /// - so it never appears as boilerplate under an ordinary empty
        /// result.
        /// </para>
        /// <para>
        /// A character the source filter has unchecked is not a match:
        /// typing another letter would still not surface it, and a hint
        /// that promises otherwise is worse than none. That is why the
        /// exclusion set is a parameter rather than assumed empty - it is
        /// the same set <see cref="SnapshotSourceFilter.UncheckedCharacters"/>
        /// carries, taken directly so the caller need not build a whole
        /// filter for a read. No id is involved: the hint names no
        /// character at all, and this tab already shows character NAMES in
        /// its own checkboxes and row breakdowns.
        /// </para>
        /// </summary>
        public static string ShortQueryCharacterHint(
            string searchText, IReadOnlyList<string> characterNames,
            ICollection<string> uncheckedCharacterNames = null)
        {
            string trimmed = (searchText ?? string.Empty).Trim();
            if (trimmed.Length == 0 || trimmed.Length >= MinCharacterSearchLength)
            {
                return null;
            }

            if (characterNames == null)
            {
                return null;
            }

            foreach (string name in characterNames)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (uncheckedCharacterNames != null && uncheckedCharacterNames.Contains(name))
                {
                    continue;
                }

                if (name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return ShortQueryCharacterHintText;
                }
            }

            return null;
        }

        /// <summary>
        /// Wording of <see cref="ShortQueryCharacterHint"/>. States the
        /// action, not the rule: "minimum query length" is this class's
        /// vocabulary, "type another letter" is the reader's.
        /// </summary>
        public const string ShortQueryCharacterHintText =
            "Type another letter to match character names.";

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
        /// against the item's own name OR - for queries of at least
        /// <see cref="MinCharacterSearchLength"/> characters - against the
        /// name of a character holding it (Feature 1 Open Question 2,
        /// resolved in favor of source-label matching; storage-location
        /// labels stay unmatched)
        /// and (b) has a positive total once <paramref name="sourceFilter"/>
        /// has excluded any unchecked sources. An item with zero quantity
        /// across the checked sources drops out of the list entirely
        /// rather than appearing as a zero-count row.
        /// <para>
        /// The two compose as a plain AND: only sources that survive the
        /// filter are consulted for the character match, so an unchecked
        /// character's rows stay hidden even when its own name is typed. A
        /// row surfaced by a character match still reports the account-wide
        /// total and full breakdown across the checked sources, not just the
        /// matched character's share - the matched character appears in the
        /// breakdown either way, and the total keeps meaning the same thing
        /// on every row in the list.
        /// </para>
        /// Rows are sorted by
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
        /// per call. Character matching costs a full source walk for every
        /// item whose name does not match, where a name-only search could
        /// skip straight past it; that is bounded above by the empty-search
        /// rebuild, which already walks every source of every item.
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
            bool searching = trimmedSearch.Length > 0;

            foreach (var kvp in itemsById)
            {
                int itemId = kvp.Key;

                // Never display raw item IDs (repo invariant).
                string name = string.IsNullOrWhiteSpace(kvp.Value.Name) ? "Unknown Item" : kvp.Value.Name;

                bool nameMatches = !searching || name.IndexOf(trimmedSearch, StringComparison.OrdinalIgnoreCase) >= 0;

                var prioritizedSources = AccountItemIndex.GetPrioritizedSources(itemId, index, activeCharacterName);
                var breakdown = new List<SnapshotSourceCount>();
                int total = 0;
                bool characterMatches = false;

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

                    if (searching && !nameMatches && !characterMatches)
                    {
                        characterMatches = CharacterNameMatches(source, trimmedSearch);
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

                if (!nameMatches && !characterMatches)
                {
                    continue;
                }

                rows.Add(new SnapshotSearchRow
                {
                    ItemId = itemId,
                    Name = name,
                    IconUrl = kvp.Value.IconUrl ?? string.Empty,

                    // From the first entry seen for this id, like Name and
                    // IconUrl: the same item in a bank slot and on a
                    // character is the same item, so any of its entries
                    // carries the same captured rarity.
                    Rarity = kvp.Value.Rarity ?? string.Empty,
                    TotalCount = total,
                    Breakdown = breakdown,
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
        /// Every character name the snapshot knows about, deduped and sorted
        /// (case-insensitive, with an ordinal tiebreak so two names differing
        /// only by case keep a deterministic order). Drives the Snapshot
        /// tab's per-character source checkboxes, so it deliberately merges
        /// both rosters the snapshot carries: the "Character:&lt;name&gt;"
        /// item sources AND CharacterDisciplines - a character holding no
        /// items at all still gets a checkbox as long as the snapshot saw it
        /// somewhere. Zero-count item entries are kept here (unlike
        /// AccountItemIndex, which drops them) for the same reason: the row
        /// lists the roster, not what happens to be carried right now.
        /// Returns an empty list, never null, for a null snapshot.
        /// </summary>
        public static List<string> CollectCharacterNames(AccountSnapshot snapshot)
        {
            var names = new List<string>();
            if (snapshot == null)
            {
                return names;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (snapshot.Items != null)
            {
                foreach (var entry in snapshot.Items)
                {
                    string source = entry?.Source;
                    if (source == null || !source.StartsWith(AccountItemIndex.CharacterSourcePrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string name = source.Substring(AccountItemIndex.CharacterSourcePrefix.Length);
                    if (name.Length > 0 && seen.Add(name))
                    {
                        names.Add(name);
                    }
                }
            }

            if (snapshot.CharacterDisciplines != null)
            {
                foreach (var discipline in snapshot.CharacterDisciplines)
                {
                    string name = discipline?.CharacterName;
                    if (!string.IsNullOrEmpty(name) && seen.Add(name))
                    {
                        names.Add(name);
                    }
                }
            }

            names.Sort((a, b) =>
            {
                int byName = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : string.CompareOrdinal(a, b);
            });
            return names;
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
        /// True when <paramref name="search"/> is at least
        /// <see cref="MinCharacterSearchLength"/> characters long and occurs
        /// (case-insensitively) in the character-name half of a
        /// "Character:&lt;name&gt;" source. The scan starts past the encoding
        /// prefix, so searching "char" matches a character actually named
        /// e.g. "Charr Hoarder" and never the internal token itself, and it
        /// takes no substring (this runs per source per item on the
        /// keystroke path).
        /// </summary>
        private static bool CharacterNameMatches(string rawSource, string search)
        {
            return search.Length >= MinCharacterSearchLength
                && rawSource.StartsWith(AccountItemIndex.CharacterSourcePrefix, StringComparison.Ordinal)
                && rawSource.IndexOf(search, AccountItemIndex.CharacterSourcePrefix.Length, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// True when the raw AccountItemIndex source string passes the
        /// currently-checked categories in <paramref name="filter"/>. A
        /// null filter is treated as "show everything" (matches the
        /// controls' own all-checked default), as is a character whose name
        /// is absent from SnapshotSourceFilter.UncheckedCharacters. A raw
        /// source string that matches none of the four known shapes
        /// (Bank/MaterialStorage/SharedInventory/Character:&lt;name&gt;) is shown regardless -
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
                var excluded = filter.UncheckedCharacters;
                if (excluded == null || excluded.Count == 0)
                {
                    return true;
                }

                return !IsExcludedCharacter(rawSource, excluded);
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
        /// True when the character-name half of a "Character:&lt;name&gt;"
        /// source appears in the exclusion set. Compares the name in place
        /// rather than taking a substring (this runs per source per item on
        /// the keystroke path), which trades the set's O(1) lookup for a
        /// scan of it - bounded by the roster, and only reached at all once
        /// the user has unchecked something. Ordinal, matching the
        /// comparer SnapshotSourceFilter's set is created with.
        /// </summary>
        private static bool IsExcludedCharacter(string rawSource, HashSet<string> excluded)
        {
            int prefixLength = AccountItemIndex.CharacterSourcePrefix.Length;
            int nameLength = rawSource.Length - prefixLength;

            foreach (string name in excluded)
            {
                if (name != null
                    && name.Length == nameLength
                    && string.CompareOrdinal(rawSource, prefixLength, name, 0, nameLength) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Display-formats a raw AccountItemIndex source string, stripping
        /// the internal "Character:" encoding prefix and spacing out the
        /// PascalCase storage-location names (e.g. "MaterialStorage" -&gt;
        /// "Material Storage") - a small polish fix so the raw internal
        /// token never reaches the UI verbatim. The underlying strings are
        /// already display-safe, not raw ids, so this is cosmetic only, not
        /// an ids-stay-internal fix. Returns "Unknown" for
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
