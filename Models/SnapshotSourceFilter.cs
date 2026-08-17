namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Which account-inventory sources the Snapshot tab's search/filter row
    /// should include (d1-snapshot-about-settings.md Feature 1).
    /// All four default to true
    /// (show everything), matching the pre-search-box tab's implicit
    /// no-filter behavior. "Characters" is a single combined toggle
    /// covering every "Character:&lt;name&gt;"-encoded source, not a
    /// per-character list (Feature 1 Open Question 1's accepted choice).
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
        public bool Characters { get; set; } = true;
    }
}
