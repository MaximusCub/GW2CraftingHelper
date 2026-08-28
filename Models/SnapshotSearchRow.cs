using System.Collections.Generic;

namespace TaimisToolbench.Models
{
    /// <summary>
    /// One grouped result row for the Snapshot tab's search list
    /// (dev/proposals/d1-snapshot-about-settings.md Feature 1): one row
    /// per matching itemId, with its account-wide total and a per-source
    /// breakdown (ordered via the existing, unmodified
    /// AccountItemIndex.GetPrioritizedSources - see
    /// Services.SnapshotSearchResultBuilder). Replaces the retired
    /// "Aggregate" checkbox's one-row-per-item-per-source rendering with
    /// this as the only, always-on behavior.
    /// </summary>
    internal class SnapshotSearchRow
    {
        public int ItemId { get; set; }

        public string Name { get; set; } = "";

        public string IconUrl { get; set; } = "";

        /// <summary>
        /// The rarity captured with this item's name and icon, or "" for a
        /// row read out of a snapshot.json written before snapshots carried
        /// rarity. The view resolves it against the session's stat cache
        /// (ItemRarityResolution) rather than reading it raw.
        /// </summary>
        public string Rarity { get; set; } = "";

        public int TotalCount { get; set; }

        public List<SnapshotSourceCount> Breakdown { get; set; } = new List<SnapshotSourceCount>();
    }

    /// <summary>
    /// One source's contribution to a <see cref="SnapshotSearchRow"/>'s
    /// total - Label is already the display-formatted source name (e.g.
    /// "Material Storage", "Character: Zaeed"), never the raw
    /// AccountItemIndex source key (repo invariant: source strings are
    /// already display names, but the raw "Character:" prefix is an
    /// internal encoding token that must never reach the UI verbatim).
    /// </summary>
    internal class SnapshotSourceCount
    {
        public string Label { get; set; } = "";

        public int Count { get; set; }
    }
}
