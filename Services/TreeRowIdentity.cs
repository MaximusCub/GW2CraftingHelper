using System;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Whether a built recipe-tree row may be repainted onto a freshly solved
    /// node or has to be rebuilt (Blish-free, unit-testable). The in-place
    /// refresh in Views/Rendering/TreeSectionController repaints only what a
    /// re-solve moves and leaves the row's icon, name text, rarity colour and
    /// caret untouched; this predicate is what makes that correct.
    ///
    /// A shared NodeId is NOT sufficient, which is the whole reason this
    /// exists: a vendor cost-component leaf's id encodes its POSITION in the
    /// chosen offer's cost lines, while its name, icon and rarity come from
    /// that line's own ItemId, so a re-solve that picks a different offer of
    /// the same shape keeps every id and every structural fact and changes only
    /// which items the lines name.
    ///
    /// A rejection costs one full rebuild, which is what every click paid
    /// before the in-place refresh existed. Being wrong costs a row that states
    /// one item and prices another. Derivation: docs/ARCHITECTURE.md, S2.7.
    /// </summary>
    internal static class TreeRowIdentity
    {
        /// <summary>
        /// True when <paramref name="fresh"/> may be painted into the row
        /// built for <paramref name="built"/>.
        /// <para>
        /// Two groups of facts. IDENTITY - item id, cost-component-ness,
        /// and the three display strings a row resolves once and never
        /// re-derives - because the refresh keeps that chrome. STRUCTURE -
        /// children count, and whether a quantity prefix exists at all -
        /// because those change which controls the row HAS rather than
        /// what they say.
        /// </para>
        /// </summary>
        public static bool SameRow(CraftingTreeNode built, CraftingTreeNode fresh)
        {
            if (built == null || fresh == null)
            {
                return false;
            }

            if (built.ItemId != fresh.ItemId)
            {
                return false;
            }

            if (built.IsCostComponent != fresh.IsCostComponent)
            {
                return false;
            }

            if (!string.Equals(built.Name, fresh.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(built.IconUrl, fresh.IconUrl, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(built.Rarity, fresh.Rarity, StringComparison.Ordinal))
            {
                return false;
            }

            if (built.Children.Count != fresh.Children.Count)
            {
                return false;
            }

            if ((built.Quantity > 0) != (fresh.Quantity > 0))
            {
                return false;
            }

            return true;
        }
    }
}
