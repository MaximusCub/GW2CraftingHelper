using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// M38 WP-23 (m38-a1-architecture.md S3b-T2 pilot): the minimal seam a
    /// section renderer needs from CraftingPlanView to participate in the
    /// M33 C2b resize-relayout contract (KNOWN-ISSUES #13/#19) without
    /// holding a reference to the view itself.
    ///
    /// CraftingPlanView implements this over its existing private
    /// _relayoutActions/_reellipsisActions registries (see the field
    /// comment on CraftingPlanView) with ZERO semantic change: both members
    /// below are a straight pass-through to the same List&lt;Action&lt;int&gt;&gt;.Add
    /// calls the inline section builders used to make directly. That means
    /// every invariant that reads those lists - the DEBUG must-register
    /// check in CreateCollapsibleSection (counts _relayoutActions before/
    /// after a section body builder runs), the DEBUG scroll-neutral assert
    /// in ReplayRelayout, and ReplayRelayout/RunReellipsis's own foreach -
    /// sees a sink-registered closure exactly as it would have seen one
    /// added inline. A section that forgets to call AddRelayout still trips
    /// the same "registered no relayout closures" warning it always did.
    ///
    /// Kept to exactly the two registries on purpose - a renderer that also
    /// needs shared chrome (e.g. CreateSectionHeader) or a static primitive
    /// (LabelHelpers/IconControls/RarityColors/CoinCurrencyRenderer) reaches
    /// those directly; they take no dependency on CraftingPlanView already,
    /// so they do not belong on this interface. This keeps the seam small
    /// enough that a future icon/coin-carrying section renderer can adopt it
    /// unchanged - AddReellipsis exists for exactly that case even though
    /// this pilot (Required Disciplines) never calls it. Verified true for
    /// Used Materials (CreateUsedMaterialRow) and Crafting Steps
    /// (CreateCraftStepRow): both depend only on the same already-extracted
    /// Views/Rendering statics this pilot uses. Shopping List's
    /// CreateShoppingRow was the one case that did NOT already satisfy that:
    /// it also needed CraftingPlanView's private static
    /// GetPillColors(PillKind, bool, out Color, out Color) (for its
    /// source-tag panel colors) and the private static helper
    /// ShoppingSourceTag(row), neither reachable from Views/Rendering at the
    /// time of the WP-23 pilot. WP-23b resolved this: GetPillColors moved to
    /// Views/Rendering/PillColors.cs (kept separate from
    /// ShoppingListSectionRenderer because CraftingPlanView.RenderDecisionPills
    /// also calls it), and ShoppingSourceTag moved directly into
    /// ShoppingListSectionRenderer (it had exactly one call site). Neither
    /// bumped a CraftingPlanView private to internal - both moves preserve
    /// the forward-only Views/Rendering -> CraftingPlanView direction the
    /// WP-21 findings fix established (see docs/KNOWN-ISSUES.md's WP-23
    /// entry). A future section renderer that needs a CraftingPlanView-private
    /// helper should follow the same precedent: extract the helper to its own
    /// Views/Rendering class (or into the section renderer itself, if it has
    /// exactly one call site) rather than reaching back into CraftingPlanView.
    ///
    /// M38 WP-23c (Crafting Steps + Required Recipes) hit two more forks of
    /// this same shape, both resolved the same way: CreateTextRow (needed by
    /// the extracted CraftingSteps body's TimegatedNotice branch, but also
    /// still called by CraftingPlanView's own default-fallback section case
    /// and CreateSummarySectionBody) moved to Views/Rendering/TextRowRenderer;
    /// CreateCTableHeaderRow (needed by both Required Disciplines and
    /// Required Recipes, the WP-23 pilot's own documented fork) moved to
    /// Views/Rendering/CTableHeaderRenderer once BOTH its callers became
    /// extracted section renderers, removing CraftingPlanView's remaining
    /// c-table-header knowledge entirely rather than leaving it as a third
    /// forward-only exception. Both new classes take this interface as a
    /// plain method parameter (not a constructor-injected field like the
    /// section renderers use) since neither is itself a section renderer -
    /// they are shared row-construction helpers with multiple callers,
    /// analogous to PillColors/RarityColors, that also need to register a
    /// relayout closure the way a section renderer's own row builders do.
    ///
    /// M38 WP-24 (m38-a2-simplify.md finding #3) added two more sink-as-
    /// plain-parameter shared helpers on this same pattern, this time
    /// factoring duplication BETWEEN already-extracted section renderers'
    /// row builders rather than resolving a stay-in-the-view fork:
    /// RowRelayoutHelpers.FinishRow (the "row panel resize + per-row extra
    /// reposition + divider resize" AddRelayout closure every row builder
    /// across Crafting Steps/Disciplines/Recipes/Shopping List/Used
    /// Materials repeated identically) and IconNameRowHelpers (the "icon +
    /// ellipsized name label, re-ellipsized at settle" shape Used
    /// Materials' and Shopping List's rows shared exactly, confirmed by
    /// constant-by-constant comparison - see each helper's own doc comment
    /// for what it does and does not cover).
    /// </summary>
    internal interface ISectionRelayoutSink
    {
        /// <summary>
        /// Registers a cheap, position/width-only closure that repositions
        /// already-built controls for a new panel width w. Replayed, in
        /// registration order, by ReplayRelayout on every resize-drag frame.
        /// Must never re-measure text, change a control's Height, or touch
        /// the scrollbar (the DEBUG assert in ReplayRelayout polices the
        /// last one) - see the _relayoutActions field comment on
        /// CraftingPlanView for the full contract.
        /// </summary>
        void AddRelayout(Action<int> closure);

        /// <summary>
        /// Registers the small subset of relayout closures that also need
        /// to re-ellipsize a truncated label for the new width. Replayed
        /// once at drag-settle by RunReellipsis, not on every tick - see
        /// the _reellipsisActions field comment on CraftingPlanView. The
        /// Required Disciplines pilot never calls this (its two
        /// DefaultFont14 labels are never truncated); it is on the
        /// interface now so a later icon/name-label section renderer does
        /// not require a breaking interface change to adopt the sink.
        /// </summary>
        void AddReellipsis(Action<int> closure);
    }
}
