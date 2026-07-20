using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Contracts
{
    /// <summary>
    /// Represents an item that <see cref="IItemSearchProvider"/> has identified as a
    /// valid plan target — an item for which <c>CraftingPlanPipeline</c> can generate
    /// a crafting plan (e.g. discipline recipes, Mystic Forge outputs).
    /// </summary>
    public class ItemSearchResult
    {
        /// <summary>GW2 API item ID. Internal-only — never display to users.</summary>
        public int ItemId { get; set; }

        /// <summary>Display name shown in the item-selection dropdown.</summary>
        public string Name { get; set; }

        /// <summary>
        /// Render-service URL for the item icon, or <c>null</c> if unavailable.
        /// The view falls back to an error texture when <c>null</c>.
        /// </summary>
        public string IconUrl { get; set; }

        /// <summary>
        /// Indicates that this item is a confirmed plan target — an item the
        /// <c>CraftingPlanPipeline</c> can resolve into a crafting plan.
        /// Providers MUST set this to <c>true</c> for every returned result;
        /// items that are not valid plan targets should not be returned at all.
        /// </summary>
        public bool IsPlanTarget { get; set; }
    }

    // TODO: Consider renaming to IPlanTargetSearchProvider if the seam's
    //       semantics expand beyond simple item search (e.g. filtering by
    //       discipline, rarity, or account unlock status).

    /// <summary>
    /// Provides searchable access to items that are valid plan targets.
    /// <para>
    /// <b>Contract:</b> Every <see cref="ItemSearchResult"/> returned by
    /// <see cref="SearchAsync"/> MUST represent an item for which
    /// <c>CraftingPlanPipeline</c> can generate a plan. Providers MUST NOT
    /// return arbitrary items with no known plan path. The UI treats results
    /// as authoritative plan targets and will attempt plan generation on
    /// user selection.
    /// </para>
    /// </summary>
    public interface IItemSearchProvider
    {
        /// <summary>
        /// Searches for plan-valid items matching <paramref name="query"/>.
        /// An empty or <c>null</c> query returns all available targets (up to
        /// <paramref name="maxResults"/>).
        /// </summary>
        /// <remarks>
        /// Implementations MUST NOT perform an arbitrary search over all GW2
        /// items. Only items that the <c>CraftingPlanPipeline</c> can resolve
        /// into a crafting plan may be returned. The provider — not the UI —
        /// is responsible for guaranteeing plan validity of every result.
        /// Implementations may also complete asynchronously; the caller
        /// (SuggestionPanel) marshals UI application of the result back to
        /// the main thread rather than assuming this runs inline.
        /// </remarks>
        /// <param name="query">
        /// Case-insensitive substring filter. Empty/null returns all items.
        /// </param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An ordered list of plan-valid items. Every entry MUST have
        /// <see cref="ItemSearchResult.IsPlanTarget"/> set to <c>true</c>.
        /// </returns>
        Task<IReadOnlyList<ItemSearchResult>> SearchAsync(
            string query, int maxResults, CancellationToken ct);
    }
}
