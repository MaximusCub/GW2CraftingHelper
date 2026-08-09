using System.Collections.Generic;
using System.Text;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Caps the "name x quantity[, name x quantity...]" requestLabel
    /// CraftingPlanView.TriggerGenerate builds for
    /// CraftingPlanPipeline.GenerateStructuredAsync's rich ModuleLog lines
    /// (W3B review-fix). Uncapped, that label joins EVERY filled item row's
    /// resolved name and is written verbatim into the start/finish/cancel/
    /// fail Info/Warn lines - a 20-row plan of long GW2 item names produces
    /// a single ~700-character ModuleLog line. Pure, Blish-free string
    /// shaping only; the caller still supplies already-formatted "name xN"
    /// entries in row order (see CraftingPlanView.TriggerGenerate).
    /// </summary>
    public static class RequestLabelFormatter
    {
        /// <summary>
        /// Entries beyond this count collapse into a single "+N more"
        /// suffix rather than being dropped silently - the reader still
        /// learns the plan's true size even when the names themselves are
        /// truncated.
        /// </summary>
        private const int MaxVisibleEntries = 3;

        /// <summary>
        /// Joins the first <see cref="MaxVisibleEntries"/> entries with
        /// ", " exactly like the pre-existing uncapped join; a 4th-or-later
        /// entry instead collapses to a trailing ", +N more". Returns an
        /// empty string for a null/empty list (matching
        /// string.Join(", ", empty) so callers need no special-case
        /// handling before falling back to their own default wording).
        /// </summary>
        public static string Format(IReadOnlyList<string> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return string.Empty;
            }

            if (entries.Count <= MaxVisibleEntries)
            {
                return string.Join(", ", entries);
            }

            var sb = new StringBuilder();
            for (int i = 0; i < MaxVisibleEntries; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(entries[i]);
            }

            int hiddenCount = entries.Count - MaxVisibleEntries;
            sb.Append(", +").Append(hiddenCount).Append(" more");
            return sb.ToString();
        }
    }
}
