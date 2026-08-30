using Blish_HUD.Controls;
using System;
using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// What an item icon says on hover, and WHY - the parameter
    /// <see cref="IconControls.CreateItemIcon"/> takes instead of an
    /// optional trailing tooltip string. The call site has to say which it
    /// means, and a factory name is what a diff shows.
    /// <para>
    /// SCOPE is the other half of the contract, and this type is where it is
    /// enforced: the hover belongs to the item's ICON and to nothing else.
    /// There is no seam that takes a Label or a row Panel, so the only
    /// control that can carry an item tooltip is the one
    /// <see cref="IconControls.CreateItemIcon"/> builds - see StampOnIconTree.
    /// </para>
    /// <para>
    /// The rich half is always DEFERRED: a stat block can land after the row
    /// was built (a plan restored from disk tops its stats up in the
    /// background), and content snapshotted at render time could never show
    /// it. See <c>TooltipFacility.ApplyRichDeferred</c>.
    /// </para>
    /// docs/ARCHITECTURE.md, "Views: relocated design narrative".
    /// </summary>
    internal readonly struct ItemIconTooltip
    {
        private readonly Func<TooltipContent> _build;
        private readonly string _plainText;

        private ItemIconTooltip(Func<TooltipContent> build, string plainText)
        {
            _build = build;
            _plainText = plainText;
        }

        /// <summary>
        /// What the control says before the rich builder runs, and what it
        /// falls back to if that builder composes nothing or throws - the
        /// item's own name. Null for a silent intent.
        /// </summary>
        internal string PlainText
        {
            get { return _plainText; }
        }

        /// <summary>
        /// THE standard item hover: the item's stat block if this session
        /// happens to hold one, headed either way by the icon+name row the
        /// game's own tooltip opens with.
        /// <paramref name="getStatBlock"/> may be null on a surface with no
        /// session stat cache to read - the header still renders, because
        /// it comes from the identity the row already had.
        /// </summary>
        internal static ItemIconTooltip ForItem(
            ItemTooltipIdentity identity, Func<ItemStatBlock> getStatBlock)
        {
            return ForItem(identity, getStatBlock, null);
        }

        /// <summary>
        /// The standard item hover plus this surface's own prose lines - a
        /// HAVE/NEED split, an acquisition hint, a source breakdown -
        /// gathered at hover time so a line that depends on the row's
        /// current width is read when it is shown, not when it was built.
        /// </summary>
        internal static ItemIconTooltip ForItem(
            ItemTooltipIdentity identity,
            Func<ItemStatBlock> getStatBlock,
            Func<IReadOnlyList<string>> extraLines)
        {
            return new ItemIconTooltip(
                () => ItemRowTooltipComposer.BuildRowContent(
                    getStatBlock == null ? null : getStatBlock(),
                    identity,
                    extraLines == null ? null : extraLines()),
                identity.Name);
        }

        /// <summary>
        /// For the surfaces whose extra lines are CONTENT rather than prose
        /// - a unit price that has to keep its coin spans - and so compose
        /// their own <see cref="TooltipContent"/>. The builder is still
        /// expected to run through
        /// <see cref="ItemRowTooltipComposer.BuildRowContent"/>, which is
        /// where the header comes from; the identity is taken here only for
        /// the plain fallback.
        /// </summary>
        internal static ItemIconTooltip Composed(
            ItemTooltipIdentity identity, Func<TooltipContent> build)
        {
            return new ItemIconTooltip(build, identity.Name);
        }

        /// <summary>
        /// THE standard wallet-currency hover: the game's own currency
        /// tooltip - icon+name, the wallet balance, the currency's prose,
        /// the type line (<see cref="CurrencyTooltipComposer"/>).
        /// <paramref name="getFacts"/> is read at hover time, so a
        /// /v2/currencies reply that lands after the row was built still
        /// reaches the box.
        /// <para>
        /// The KIND is the caller's to choose and it must come from the id
        /// space the caller drew the icon from, never from the name:
        /// "Gaeting Crystal" is wallet currency 77 AND item 104026,
        /// one in-game good in two id spaces, so a name lookup cannot tell
        /// which tooltip it is owed. A good the module lists among its
        /// currencies but which is really an ITEM - Crystalline Ore 46682
        /// - takes <see cref="ForItem(ItemTooltipIdentity, Func{ItemStatBlock})"/>
        /// instead. Views/SettingsTabContent's <c>IsBarterItem</c> is the
        /// discriminator that already carries this distinction.
        /// </para>
        /// </summary>
        internal static ItemIconTooltip ForCurrency(
            string name, Func<CurrencyTooltipFacts> getFacts)
        {
            if (getFacts == null)
            {
                throw new ArgumentNullException(nameof(getFacts));
            }

            return new ItemIconTooltip(
                () => CurrencyTooltipComposer.BuildContent(getFacts()), name);
        }

        /// <summary>
        /// Deliberately silent, with the reason named at the call site.
        /// Adding a reason to <see cref="ItemIconSilence"/> is the act of
        /// the commit that needs one.
        /// </summary>
        internal static ItemIconTooltip None(ItemIconSilence why)
        {
            // Nothing is drawn from the reason, and that is the point: it
            // exists so the silence is a statement in the diff rather than
            // an absent argument.
            _ = why;
            return new ItemIconTooltip(null, null);
        }

        /// <summary>
        /// A Panel laid OVER an icon's art - the tree's dimming scrim for a
        /// reference branch. It is part of the icon as far as the cursor is
        /// concerned, and Blish resolves on the deepest control, so leaving
        /// it unstamped puts a hole in the middle of the icon.
        /// </summary>
        internal void StampOnIconOverlay(Panel overlay)
        {
            StampOnIconTree(overlay);
        }

        /// <summary>
        /// The icon and everything nested inside it, for
        /// <see cref="IconControls"/> to call as it builds one. Blish
        /// resolves a tooltip on the deepest control under the cursor and
        /// never bubbles, so the frame, its art square and the missing-icon
        /// placeholder mark each need their own.
        /// <para>
        /// SCOPE: the icon, and no further. The module used to stamp the
        /// row panel and every label on it, precisely BECAUSE Blish does
        /// not bubble and the gaps otherwise answered nothing - which meant
        /// a wide row popped the item's tooltip over its counts, its
        /// prices, its timestamps and its empty middle. Those gaps SHOULD
        /// answer nothing. Owner ruling: "i want just the icon for the item
        /// tooltip."
        /// </para>
        /// </summary>
        internal void StampOnIconTree(Control iconTree)
        {
            if (iconTree == null)
            {
                return;
            }

            // The icon's own note ("no icon available for this entry") is
            // already on the tree and is worth more than silence, so a
            // silent intent leaves the plain layer alone rather than
            // clearing it.
            if (_build == null)
            {
                return;
            }

            IconControls.ApplyRichDeferredToIconTree(iconTree, _build);
        }
    }

    /// <summary>
    /// Why an item icon shows nothing on hover. One member per real reason;
    /// there is deliberately no "other".
    /// </summary>
    internal enum ItemIconSilence
    {
        /// <summary>The icon is being drawn INSIDE a tooltip box. A tooltip
        /// that spawns a tooltip has nowhere to put it.</summary>
        DrawnInsideATooltip,

        /// <summary>The icon sits in an open dropdown list whose remaining
        /// rows are what the reader is scanning; a hover box would cover
        /// the choices it is meant to help them make.</summary>
        WouldCoverTheListItSitsIn,
    }
}
