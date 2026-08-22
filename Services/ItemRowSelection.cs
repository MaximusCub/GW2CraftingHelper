using System;
using System.Collections.Generic;
using GW2CraftingHelper.Contracts;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure decisions that keep an input row's resolved item in step with
    /// the text its search box actually shows (Blish-free, unit-testable).
    /// A row carries both an item id and the name the user sees, but only a
    /// suggestion pick ever sets the two together - free typing afterwards
    /// leaves the id behind unless something invalidates it, which is how a
    /// box reading one item could generate a plan for another.
    /// </summary>
    public static class ItemRowSelection
    {
        /// <summary>Shown when every row is genuinely empty.</summary>
        public const string NoItemsStatus = "Select at least one item before generating.";

        /// <summary>
        /// Shown when a row has text that resolved to nothing - the typed
        /// name is not a plan target, or was only partly typed.
        /// </summary>
        public const string UnmatchedTextStatus =
            "No item matched what you typed - pick an item from the suggestion list.";

        /// <summary>
        /// True when <paramref name="resolvedItemId"/> no longer describes
        /// what the search box reads and must be dropped. Case and
        /// surrounding whitespace do not count as divergence: the pick that
        /// set the id is re-matched case-insensitively at generate time, so
        /// retyping the same name in another case still means the same item.
        /// </summary>
        public static bool SelectionIsStale(int? resolvedItemId, string resolvedName, string boxText)
        {
            if (!resolvedItemId.HasValue)
            {
                return false;
            }

            return !NamesMatch(resolvedName, boxText);
        }

        /// <summary>
        /// The one comparison rule for item names in the input strip:
        /// trimmed, case-insensitive, null treated as empty.
        /// </summary>
        public static bool NamesMatch(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The search result whose name is exactly what was typed, or null
        /// when the text only partly matches (or matches nothing). Used to
        /// adopt a full name someone typed without ever opening the
        /// suggestion list; a partial name must stay unresolved rather than
        /// silently generate a plan for whichever result ranked first.
        /// </summary>
        public static ItemSearchResult FindExactNameMatch(IReadOnlyList<ItemSearchResult> results, string text)
        {
            if (results == null)
            {
                return null;
            }

            string wanted = (text ?? string.Empty).Trim();
            if (wanted.Length == 0)
            {
                return null;
            }

            foreach (var result in results)
            {
                if (result == null)
                {
                    continue;
                }

                if (NamesMatch(result.Name, wanted))
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// What to tell the user when a Generate produced no request items:
        /// blank rows and typed-but-unmatched rows are different mistakes
        /// and need different fixes.
        /// </summary>
        public static string EmptyRequestStatus(bool anyRowHasText)
        {
            return anyRowHasText ? UnmatchedTextStatus : NoItemsStatus;
        }
    }
}
