namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Severity of a ModuleLogEntry. Four levels, matching Blish's own
    /// Logger level names 1:1 (Debug/Info/Warn/Error are the only levels
    /// used anywhere in this codebase's existing Logger.* call sites - no
    /// Fatal/Trace exist, so four levels covers current usage without
    /// inventing unused ones - see d2-log-system.md Section 4.1). Ordinal
    /// order doubles as severity order for the Log tab's "minimum severity"
    /// filter (Error+/Warn+/Info+/Debug+) and for ModuleLog's own file-sink
    /// floor check.
    /// </summary>
    public enum ModuleLogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }
}
