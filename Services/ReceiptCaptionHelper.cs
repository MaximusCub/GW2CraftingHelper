using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure "which child index gets which caption" logic for the UI-bundle
    /// milestone's Feature C (receipt/what-if captions). Blish-free by
    /// design so it can be exercised by a real test over plain
    /// CraftingTreeNode objects, independent of the Views/Rendering render
    /// pass that consumes it.
    /// <para>
    /// A vendor-selected node's Children is a STACK exactly when the
    /// cost-component-leaf synthesis AND the "what it would cost to craft
    /// instead" reference branch both fired for the same node
    /// (CraftingTreeBuilder.BuildNode's componentLeaves != null &amp;&amp;
    /// wantsReferenceBranch branch): the leading run of children are
    /// synthesized cost-component leaves (IsCostComponent == true), the
    /// remainder are the reference branch's own recipe ingredients
    /// (IsCostComponent == false). node.IsReferenceBranch is set true in
    /// that same branch, so "IsReferenceBranch AND the first child is a
    /// cost component" is exactly the stacked case - detectable from the
    /// node alone, with no new model field needed.
    /// </para>
    /// <para>
    /// CAUTION (per the milestone spec): tree row heights flow through
    /// PlanContentHeightMath's tree arm, which counts exactly
    /// node.Children.Count rows per level - a frozen file this package
    /// does not touch. This helper never adds, removes, or reorders a
    /// node's Children; it only says which of the EXISTING children (by
    /// index) a caption belongs in front of, so the caller can render the
    /// caption as an extra tooltip line on that child's own row instead of
    /// inserting a new row the height math would not know how to count -
    /// see TreeSectionController.RenderTreeNode's captionText parameter.
    /// </para>
    /// </summary>
    public static class ReceiptCaptionHelper
    {
        public const string VendorPriceCaption = "Vendor price:";
        public const string CraftReferenceCaption = "If crafted instead:";

        /// <summary>
        /// The index of the first reference-branch child (the first child
        /// AFTER the leading run of cost-component leaves), or -1 when this
        /// node is not the stacked "both groups present" case at all (only
        /// component leaves, only a reference branch, or neither).
        /// </summary>
        public static int ComputeCaptionSplitIndex(CraftingTreeNode node)
        {
            // Children[0]/Children[index] are dereferenced via
            // ?. below rather than assumed non-null - CraftingTreeBuilder
            // never appends a null child today, but this method already
            // defends against upstream invariant drift elsewhere (see the
            // "unreachable in production" tail comment below), and an
            // unguarded null entry here would NRE out of the tree render
            // path, taking out the whole Recipe Tree section rather than
            // just one caption.
            if (node?.Children == null || node.Children.Count == 0 ||
                !node.IsReferenceBranch || node.Children[0]?.IsCostComponent != true)
            {
                return -1;
            }

            int index = 0;
            while (index < node.Children.Count && node.Children[index]?.IsCostComponent == true)
            {
                index++;
            }

            // A pure component-leaf list (no reference-branch children
            // actually appended after it) is not a real split - nothing to
            // caption as "If crafted instead:" since that group would be
            // empty. Unreachable in production today (IsReferenceBranch is
            // only ever set true alongside a non-empty referenceChildren
            // append - see CraftingTreeBuilder.BuildNode), guarded here so
            // this helper's own contract does not depend on that upstream
            // invariant holding forever.
            return index < node.Children.Count ? index : -1;
        }

        /// <summary>
        /// The caption text for a given child index, given the node's own
        /// split index (ComputeCaptionSplitIndex's return value, computed
        /// once per node and reused across every child index) - null when
        /// that index starts neither group.
        /// </summary>
        public static string CaptionForChildIndex(int splitIndex, int childIndex)
        {
            if (splitIndex < 0)
            {
                return null;
            }

            if (childIndex == 0)
            {
                return VendorPriceCaption;
            }

            if (childIndex == splitIndex)
            {
                return CraftReferenceCaption;
            }

            return null;
        }
    }
}
