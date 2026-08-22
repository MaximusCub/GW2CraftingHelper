using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The minimal seam a section renderer needs from CraftingPlanView to
    /// participate in the resize-relayout contract (docs/KNOWN-ISSUES.md
    /// #13/#19) without holding a reference to the view itself.
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
    /// Kept to exactly the two registries (plus the read-only
    /// RelayoutCount and the settle-time rebuild request, both of which
    /// concern the same two lists' replay) on purpose - a renderer that
    /// also needs shared
    /// chrome (e.g. CreateSectionHeader) or a static primitive
    /// (LabelHelpers/IconControls/RarityColors/CoinCurrencyRenderer) reaches
    /// those directly; they take no dependency on CraftingPlanView already,
    /// so they do not belong on this interface. A section renderer that
    /// needs a CraftingPlanView-private helper should extract the helper to
    /// its own Views/Rendering class (or into the section renderer itself,
    /// if it has exactly one call site) rather than reaching back into
    /// CraftingPlanView, preserving the forward-only Views/Rendering ->
    /// CraftingPlanView dependency direction (see docs/KNOWN-ISSUES.md's
    /// WP-23 entry). Shared row-construction helpers with multiple callers
    /// (TextRowRenderer, CTableHeaderRenderer, RowRelayoutHelpers,
    /// IconNameRowHelpers) take this interface as a plain method parameter
    /// rather than a constructor-injected field, since none of them is
    /// itself a section renderer.
    ///
    /// RelayoutCount exists only because TreeSectionController.
    /// CreateTreeSection carries the same DEBUG must-register assert as
    /// CraftingPlanView.CreateCollapsibleSection but is not inside
    /// CraftingPlanView to read _relayoutActions directly. Kept read-only
    /// and additive to AddRelayout/AddReellipsis rather than widening
    /// either of those - this is observation, not registration.
    /// <para>See docs/ARCHITECTURE.md section 5.</para>
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
        /// the _reellipsisActions field comment on CraftingPlanView.
        /// </summary>
        void AddReellipsis(Action<int> closure);

        /// <summary>
        /// Asks for one full RenderPlan rebuild once the settle pass has
        /// finished, for the case a re-ellipsis closure cannot honour its
        /// no-height-change contract at the settled width: the Notes
        /// section builds one fixed-height row per WRAPPED LINE, so a
        /// width that changes a note's line count changes the section's
        /// height, which only a rebuild may do. Called from inside a
        /// re-ellipsis closure; the rebuild is deferred because RenderPlan
        /// clears the very registry RunReellipsis is iterating.
        /// <para>
        /// Deliberately a request, not a rebuild: this stays a
        /// registration-shaped seam onto CraftingPlanView's own state, and
        /// the view decides when (and whether) to honour it, with the
        /// scroll preservation a rebuild needs. A renderer whose closures
        /// keep every row height fixed - every other section - never calls
        /// it.
        /// </para>
        /// </summary>
        void RequestRerenderAfterSettle();

        /// <summary>
        /// How many relayout closures are registered right now -
        /// see the interface doc comment above for why this exists (a
        /// DEBUG-only must-register assert moved out of CraftingPlanView
        /// alongside TreeSectionController).
        /// </summary>
        int RelayoutCount { get; }
    }
}
