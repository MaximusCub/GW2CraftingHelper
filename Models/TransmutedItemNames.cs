using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TaimisToolbench.Models
{
    /// <summary>
    /// The skin names the copies of one item id wear, for a Snapshot row
    /// that sums every one of those copies into a single line. Built by
    /// Services.TransmutedNameIndex, which owns the rule.
    /// </summary>
    internal sealed class TransmutedItemNames
    {
        // Wrapped, not a bare List: every instance built without names
        // shares this one, so a caller that cast AllNames back to
        // List<string> would be writing into all of them.
        private static readonly IReadOnlyList<string> NoNames =
            new ReadOnlyCollection<string>(new List<string>());

        internal TransmutedItemNames(TransmutedSkin display, IReadOnlyList<string> allNames)
        {
            Display = display ?? TransmutedSkin.None;
            AllNames = allNames ?? NoNames;
        }

        /// <summary>
        /// The skin the row shows in place of the item's own name and
        /// icon, or <see cref="TransmutedSkin.None"/> when the copies do
        /// not all wear the same skin.
        /// </summary>
        public TransmutedSkin Display { get; }

        /// <summary>
        /// Every distinct skin name any copy wears, in first-seen order.
        /// Search reads this rather than <see cref="Display"/>, so a name
        /// the row heading does not show still finds the item.
        /// </summary>
        public IReadOnlyList<string> AllNames { get; }
    }
}
