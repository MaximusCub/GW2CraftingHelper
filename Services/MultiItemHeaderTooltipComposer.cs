using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// What the plan header's overflow marker says: the batch items its
    /// icon run could not fit, one per line, each as the icon+name row
    /// every item tooltip in this module opens with - so a name keeps its
    /// rarity colour and an item whose icon never loaded still draws the
    /// neutral empty-slot square.
    /// <para>
    /// Blish-free (repo invariant) alongside the other tooltip composers,
    /// so which items the marker stands for - and the cap that keeps a
    /// large batch's list on screen - is unit-testable without a live
    /// control. <see cref="MultiItemHeaderLayout"/> decides WHERE the
    /// marker goes; this decides what it answers.
    /// </para>
    /// </summary>
    internal static class MultiItemHeaderTooltipComposer
    {
        /// <summary>
        /// Nothing caps how many items a plan may request, and the rich
        /// surface clamps a tooltip's POSITION, not its height - so an
        /// uncapped list would run off the bottom of the screen and take
        /// its own last entries with it. Past this many, the tail becomes a
        /// count.
        /// </summary>
        public const int MaxListedItems = 12;

        /// <summary>
        /// The items from <paramref name="firstHidden"/> onward. Empty
        /// content when nothing is hidden, which is also what the marker
        /// draws in that state - the two agree by construction rather than
        /// by the caller remembering to check.
        /// </summary>
        public static TooltipContent BuildHiddenItemsContent(
            IReadOnlyList<PlanHeaderItem> items, int firstHidden)
        {
            if (items == null || firstHidden < 0 || firstHidden >= items.Count)
            {
                return TooltipContent.Empty;
            }

            int hidden = items.Count - firstHidden;
            int overflow = hidden > MaxListedItems ? hidden - MaxListedItems : 0;
            int last = items.Count - overflow;

            var builder = new TooltipContentBuilder();
            for (int i = firstHidden; i < last; i++)
            {
                var item = items[i];
                builder.Header(
                    item?.IconUrl, item?.Name,
                    TooltipHeaderSubject.ItemOfRarity(item?.Rarity));
            }

            if (overflow > 0)
            {
                builder.Styled(
                    "and " + StatusText.Count(overflow, "more item"), TooltipSpanRole.Muted);
            }

            return builder.Build();
        }
    }
}
