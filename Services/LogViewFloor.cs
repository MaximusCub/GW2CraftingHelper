namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure "Clear View" floor arithmetic for the Log tab.
    /// Blish-free by design so it can be
    /// unit-tested directly - LogTabContent itself cannot be (a Blish HUD
    /// view; tests must never reference Blish HUD per repo rules), and the
    /// floor's storage (Module._logViewClearedBeforeVersion - see that
    /// field's own doc comment) is a plain long with no logic of its own
    /// worth testing beyond this comparison.
    ///
    /// The floor is a ring Version snapshot taken at "Clear View" click
    /// time (Module._logViewClearedBeforeVersion = ModuleLog.Version, set
    /// via LogTabContent.ClearView); an entry is hidden from the CURRENT
    /// view iff its own absolute ring index falls strictly before that
    /// snapshot - everything at or after it, including every entry that
    /// arrives later, stays visible. The ring buffer and the on-disk log
    /// file are both untouched by this (see ModuleLog.Clear/
    /// ModuleLogStore.DeleteAll for the two genuinely destructive
    /// operations this deliberately is not).
    /// </summary>
    public static class LogViewFloor
    {
        public static bool IsVisible(long absoluteIndex, long clearedBeforeVersion)
        {
            return absoluteIndex >= clearedBeforeVersion;
        }
    }
}
