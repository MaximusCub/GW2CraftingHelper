using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Which account-inventory sources the Snapshot tab's search/filter row
    /// should include (d1-snapshot-about-settings.md Feature 1). The three
    /// storage locations default to true (show everything), matching the
    /// pre-search-box tab's implicit no-filter behavior.
    /// <para>
    /// Characters are carried as an EXCLUSION set of bare character names
    /// (no "Character:" encoding prefix), resolving Feature 1 Open Question
    /// 1 in favor of per-character checkboxes. Exclusion rather than
    /// inclusion is what makes a character absent from the set visible, so a
    /// character that appears in a fresh snapshot defaults to shown without
    /// this type ever needing to know the account's roster. Ordinal
    /// comparison: the names are the same strings AccountItemIndex encodes
    /// its "Character:&lt;name&gt;" source keys from, so exact match is the
    /// only meaningful one.
    /// </para>
    /// <para>
    /// Deliberately a plain data carrier with no behavior of its own -
    /// matching/mapping this against a raw AccountItemIndex source string
    /// lives in Services.SnapshotSearchResultBuilder instead, keeping this
    /// Models type free of a Services-namespace dependency (no existing
    /// Models type references Services; see AccountItemIndex/
    /// Gw2AccountSnapshotService for where the raw source strings
    /// themselves are defined).
    /// </para>
    /// </summary>
    public class SnapshotSourceFilter
    {
        public bool Bank { get; set; } = true;
        public bool MaterialStorage { get; set; } = true;
        public bool SharedInventory { get; set; } = true;
        public HashSet<string> UncheckedCharacters { get; set; } = new HashSet<string>(StringComparer.Ordinal);
    }
}
