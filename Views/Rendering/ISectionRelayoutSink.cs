using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The minimal seam a section renderer needs from CraftingPlanView to
    /// participate in the resize-relayout contract (KNOWN-ISSUES
    /// #13/#19) without holding a reference to the view itself.
    ///
    /// Kept to the two registries plus the read-only RelayoutCount and the
    /// settle-time rebuild request, which concern the same two lists'
    /// replay. Shared chrome (CreateSectionHeader) and the static
    /// primitives (LabelHelpers/IconControls/RarityColors/
    /// CoinCurrencyRenderer) are called directly; a renderer that needs a
    /// CraftingPlanView-private helper extracts it into Views/Rendering
    /// rather than reaching back into the view (KNOWN-ISSUES #39). Shared
    /// row-construction helpers with several callers (TextRowRenderer,
    /// CTableHeaderRenderer, RowRelayoutHelpers, IconNameRowHelpers) take
    /// this interface as a method parameter rather than a
    /// constructor-injected field, since none of them is itself a section
    /// renderer.
    ///
    /// RelayoutCount is observation, not registration: TreeSectionController
    /// carries CreateCollapsibleSection's DEBUG must-register assert but
    /// cannot read _relayoutActions directly.
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
        /// How many relayout closures are registered right now - read by
        /// TreeSectionController's DEBUG must-register assert.
        /// </summary>
        int RelayoutCount { get; }
    }
}
