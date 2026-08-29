using System;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// The Recipe Tree actions and per-plan state, handed from
    /// TreeSectionController (which owns the override/ignore state they act
    /// on) to CraftingPlanView (which owns the non-scrolling strip their
    /// buttons and chips live in). A fresh instance is published on every
    /// tree render and dropped by TreeSectionController.ResetTreeRenderState,
    /// so a command can never outlive the controls it was built against.
    /// <para>
    /// The would-change predicates are answered at CLICK time, never per
    /// render: two of them build a whole preset to compare against, which is
    /// a bounded tree walk - cheap enough for a click, wasteful sixty times
    /// a second.
    /// </para>
    /// Why the buttons left the section header, and why the predicates exist
    /// at all: docs/ARCHITECTURE.md, "Views: relocated design narrative".
    /// </summary>
    internal sealed class TreeToolbarCommands
    {
        internal Action BestPath;
        internal Action CraftAll;
        internal Action BuyAll;
        internal Action ExpandAll;
        internal Action CollapseAll;

        /// <summary>
        /// Drops every manual decision and re-solves with the solver's own
        /// choices. MEASURED: this is byte-for-byte what
        /// <see cref="BestPath"/> does (TreeSectionController.
        /// ApplyBestPathPreset clears the same dictionary and re-solves) -
        /// the two differ only in the status line they write and the
        /// dialog they ask. See KNOWN-ISSUES #59.
        /// </summary>
        internal Action ClearOverrides;

        /// <summary>
        /// Drops every ignore mark and re-solves. Ignore marks and
        /// decision overrides are independent: no preset touches ignores,
        /// and this touches no decision.
        /// </summary>
        internal Action ClearIgnored;

        internal Func<int> GetOverrideCount;
        internal Func<int> GetIgnoredCount;

        /// <summary>
        /// Whether a local re-solve is possible at all on the current
        /// plan. False only for a plan restored without its solve context,
        /// which renders and shows its toolbar but can apply nothing.
        /// The confirms read it BEFORE asking: a dialog whose action
        /// cannot run is the "dialog that protects nothing" the matrix
        /// exists to avoid.
        /// </summary>
        internal Func<bool> CanReSolve;

        /// <summary>
        /// Whether the preset would change the plan: true it would, false
        /// it is already applied, NULL it cannot be answered because this
        /// plan has no solve context to build a preset from.
        /// <para>
        /// Tri-state on purpose. Collapsing null into false makes a click
        /// on an UNAVAILABLE action report the no-op line - "Already
        /// crafting everything craftable" is a confident statement about a
        /// plan nothing has examined, on the one line this milestone
        /// rebuilt around a click that does nothing having to say why.
        /// </para>
        /// </summary>
        internal Func<bool?> CraftAllWouldChange;
        internal Func<bool?> BuyAllWouldChange;
    }
}
