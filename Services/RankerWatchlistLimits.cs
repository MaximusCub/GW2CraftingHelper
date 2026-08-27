namespace TaimisToolbench.Services
{
    public static class RankerWatchlistLimits
    {
        /// <summary>
        /// Refresh cost is roughly linear in the UNION of distinct items
        /// across the whole list, and 25 legendaries is already a ~20s first
        /// refresh of a session. A cap the user cannot raise is honest about
        /// that; a setting would let them configure their way into a Refresh
        /// that looks hung.
        ///
        /// The priority cascade does not move this number - it adds no solves,
        /// only per-slot ledger arithmetic.
        /// </summary>
        public const int MaxEntries = 25;
    }
}
