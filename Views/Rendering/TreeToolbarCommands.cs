using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The five Recipe Tree actions, handed from TreeSectionController (which
    /// owns the override/expansion state they mutate) to CraftingPlanView
    /// (which owns the non-scrolling strip their buttons now live in).
    /// <para>
    /// The buttons used to sit in the tree's own section header, inside the
    /// scroll flow - so on a long plan, the moment Collapse All became
    /// useful was the moment it had scrolled off screen. The buttons moved
    /// out; the state they act on could not follow, hence this seam. A
    /// fresh instance is published on every tree render and dropped by
    /// TreeSectionController.ResetTreeRenderState, so a command can never
    /// outlive the controls it was built against.
    /// </para>
    /// </summary>
    internal sealed class TreeToolbarCommands
    {
        internal Action BestPath;
        internal Action CraftAll;
        internal Action BuyAll;
        internal Action ExpandAll;
        internal Action CollapseAll;
    }
}
