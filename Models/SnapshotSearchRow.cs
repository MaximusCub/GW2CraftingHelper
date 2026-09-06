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

        /// <summary>
        /// What the row calls the item. This is the skin's name when every
        /// copy wears the same skin, because that is the name the game
        /// shows; see Services.TransmutedNameIndex.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// The skin name behind <see cref="Name"/>, or "" when the row
        /// shows the item's own name. The tooltip needs the two apart: it
        /// prints the skin's name as the heading and the item's own name
        /// under a "Transmuted" line, the way the game does.
        /// </summary>
        public string SkinName { get; set; } = "";

        public string IconUrl { get; set; } = "";

        /// <summary>
        /// The rarity captured with this item's name and icon, or "" for a
        /// row read out of a snapshot.json written before snapshots carried
        /// rarity. The view resolves it against the session's stat cache
        /// (ItemRarityResolution) rather than reading it raw.
        /// </summary>
        public string Rarity { get; set; } = "";

        public int TotalCount { get; set; }

        /// <summary>
        /// Every place holding some of <see cref="TotalCount"/>, in the
        /// order AccountItemIndex.GetPrioritizedSources produced. Not
        /// display text: Services.SnapshotHoldLine turns the whole list into
        /// one line, because whether a place prints its count depends on the
        /// other places in the same list.
        /// </summary>
        public List<SnapshotHoldLocation> Breakdown { get; set; } = new List<SnapshotHoldLocation>();
    }
}
