using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The ONE shape an item row's tooltip has, wherever the row lives -
    /// recipe tree, Used Materials, Shopping List, Snapshot results: the
    /// item's stat block first, then a blank, then whatever that particular
    /// surface has to add (a unit price, a HAVE/NEED split, an acquisition
    /// hint).
    ///
    /// <para>
    /// The stat block already OPENS with the item's full name in its rarity
    /// colour, so a row whose label is ellipsized does NOT also prepend the
    /// name - that would show it twice. The name line is emitted only when
    /// there is no stat block to open with, which is the pre-stats fallback
    /// every one of these surfaces had.
    /// </para>
    /// <para>
    /// Blish-free (repo invariant), so the exact line-by-line contract each
    /// surface shows is unit-testable without a live control.
    /// </para>
    /// </summary>
    internal static class ItemRowTooltipComposer
    {
        /// <summary>
        /// The core, for a caller that already has its stat block composed
        /// (the recipe tree, whose id-space gate decides whether the row's
        /// numeric id is an item id at all) and whose extra lines are
        /// CONTENT rather than prose - a line carrying a coin amount keeps
        /// its coin span instead of being spelled out as "1g 23s 45c".
        /// </summary>
        public static TooltipContent BuildRowContent(
            TooltipContent statContent,
            string fullName,
            bool nameTruncated,
            TooltipContent extraContent)
        {
            var builder = new TooltipContentBuilder();

            if (statContent != null && !statContent.IsEmpty)
            {
                builder.Append(statContent);
            }
            else if (nameTruncated && !string.IsNullOrEmpty(fullName))
            {
                builder.Text(fullName).EndLine();
            }

            if (extraContent != null && !extraContent.IsEmpty)
            {
                // Separator, not a bare blank: it is a no-op on a builder
                // that is still empty, so a row with plan lines and no
                // stats never opens on a blank row.
                builder.Separator().Append(extraContent);
            }

            return builder.Build();
        }

        /// <summary>A stat block plus prose-only extras, for the surfaces
        /// whose additions are plain sentences (a hint, a HAVE/NEED
        /// split).</summary>
        public static TooltipContent BuildRowContent(
            ItemStatBlock stats,
            string fullName,
            bool nameTruncated,
            IReadOnlyList<string> extraLines)
        {
            var extras = new TooltipContentBuilder();
            if (extraLines != null)
            {
                foreach (var line in extraLines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        extras.Text(line).EndLine();
                    }
                }
            }

            return BuildRowContent(
                ItemStatTooltipComposer.BuildContent(stats), fullName, nameTruncated, extras.Build());
        }
    }
}
