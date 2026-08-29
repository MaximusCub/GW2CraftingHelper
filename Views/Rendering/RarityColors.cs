using Microsoft.Xna.Framework;

namespace TaimisToolbench.Views.Rendering
{
    internal static class RarityColors
    {
        // Rarity palette MEASURED from lossless live captures (median over
        // peak text-ink pixels of the tooltip name line; fidelity audit
        // sections 4.5 / 8.1, fix F10):
        //   Fine       (85, 153, 255)  #59F  live/s07, live/eq-weapon-full
        //   Masterwork (51, 204, 17)   #3C1  live2/p05, live2/p03
        //   Rare       (255, 221, 34)  #FD2  live2/q-food3, live2/k-1
        //                                    (name AND the "Rare" word)
        //   Exotic     (255, 170, 0)   #FA0  live/s02
        //   Ascended   (255, 68, 136)  #F48  live2/q-crystal
        //   Legendary  (153, 51, 255)  #93F  live/eq-weapon-full
        //   Basic      (255, 255, 255) #FFF  live2/f-ore
        //   Junk       (170, 170, 170) #AAA  live3/red-festival-lantern
        //                                    (68638, API rarity Junk; 289
        //                                    ink px, median exactly #AAA)
        // Every measured value is an exact multiple of 17 - GW2 defines the
        // palette in #RGB hex shorthand. All eight rarities are now
        // capture-measured; Junk's 2026-08-26 measurement landed on the
        // value previously inferred.

        /// <summary>
        /// Rarity palette for icon borders - the measured live palette
        /// above. Unknown/absent rarity renders a neutral dark grey -
        /// never guess a rarity.
        /// </summary>
        internal static Color GetRarityBorderColor(string rarity)
        {
            switch (rarity)
            {
                case "Junk": return new Color(170, 170, 170);
                // Deliberately NOT white: a white border reads as borderless
                // next to the tinted frames around it (this row's icon frame
                // in particular sits beside Fine/Rare/etc. frames that are
                // clearly colored). Distinct from the (60, 60, 60)
                // unknown/absent-rarity fallback below - deliberate.
                case "Basic": return new Color(90, 90, 90);
                case "Fine": return new Color(85, 153, 255);
                case "Masterwork": return new Color(51, 204, 17);
                case "Rare": return new Color(255, 221, 34);
                case "Exotic": return new Color(255, 170, 0);
                case "Ascended": return new Color(255, 68, 136);
                case "Legendary": return new Color(153, 51, 255);
                default: return new Color(60, 60, 60);
            }
        }

        /// <summary>
        /// THE currency frame, for every currency and coin icon in the
        /// module - there is no rarity to look up and no second opinion to
        /// have, so this takes no argument and every currency site reaches
        /// it through <see cref="ItemIconFrame.Currency"/>.
        /// <para>
        /// A third grey, distinct from the two above on purpose: the (60,
        /// 60, 60) unknown-rarity fallback is nearly invisible on the dark
        /// window - which is what "these icons have no border at all" meant
        /// in the field - and 90 is spoken for by Basic, which sits beside
        /// real rarity frames and must not be confused with one. 100 reads
        /// as a deliberate edge without competing with the tinted frames
        /// around it.
        /// </para>
        /// </summary>
        internal static Color GetCurrencyBorderColor()
        {
            return new Color(100, 100, 100);
        }

        /// <summary>
        /// Rarity palette for item NAME text - the measured live palette
        /// above, replacing the GW2 wiki dark-skin values the module
        /// shipped with (web-legibility-tuned and measurably desaturated
        /// relative to the live game). Unknown/absent rarity renders a
        /// neutral light grey - never guess.
        /// </summary>
        internal static Color GetRarityNameColor(string rarity)
        {
            switch (rarity)
            {
                case "Junk": return new Color(170, 170, 170);
                case "Basic": return new Color(255, 255, 255);
                case "Fine": return new Color(85, 153, 255);
                case "Masterwork": return new Color(51, 204, 17);
                case "Rare": return new Color(255, 221, 34);
                case "Exotic": return new Color(255, 170, 0);
                case "Ascended": return new Color(255, 68, 136);
                case "Legendary": return new Color(153, 51, 255);
                default: return new Color(200, 200, 200);
            }
        }
    }

    /// <summary>
    /// What colour an item icon's frame is, and WHY - the parameter
    /// IconControls.CreateItemIcon takes instead of a bare rarity string.
    ///
    /// <para>
    /// A string parameter cannot tell "this row has no rarity" apart from
    /// "nobody looked", so a call site that silently passed null got the
    /// neutral frame and no reviewer could see the difference. That is how
    /// the Snapshot tab shipped hundreds of grey frames next to two gold
    /// ones. Here the call site has to say which it means, and a factory
    /// name is what a diff shows.
    /// </para>
    /// </summary>
    internal readonly struct ItemIconFrame
    {
        private readonly Color _color;

        private ItemIconFrame(Color color)
        {
            _color = color;
        }

        /// <summary>The frame colour to paint.</summary>
        internal Color Color
        {
            get { return _color; }
        }

        /// <summary>
        /// The item's RESOLVED rarity - what
        /// <c>ItemRarityResolution.Resolve</c> returned after looking in
        /// every place this surface has. A null return from that policy is
        /// legitimately unknown and lands on the neutral frame; a caller
        /// that has NOT looked should not be calling this.
        /// </summary>
        internal static ItemIconFrame ForRarity(string resolvedRarity)
        {
            return new ItemIconFrame(RarityColors.GetRarityBorderColor(resolvedRarity));
        }

        /// <summary>
        /// An item whose rarity this surface structurally cannot know - the
        /// search suggestion list, whose provider returns id/name/icon and
        /// nothing else. Neutral, and the call site is on record that the
        /// gap is in the DATA, not in the wiring.
        /// </summary>
        internal static ItemIconFrame UnknownRarity()
        {
            return new ItemIconFrame(RarityColors.GetRarityBorderColor(null));
        }

        /// <summary>
        /// The subject has no rarity to resolve at all - a currency, a coin
        /// denomination. Not the unknown-rarity frame: the call site is on
        /// record that nothing is missing, and it gets the module's one
        /// currency frame (<see cref="RarityColors.GetCurrencyBorderColor"/>)
        /// rather than a per-surface grey.
        /// </summary>
        internal static ItemIconFrame Currency()
        {
            return new ItemIconFrame(RarityColors.GetCurrencyBorderColor());
        }

        /// <summary>
        /// A colour the call site owns for a reason it states: the dimmed
        /// grey of a not-crafted subtree row, the game's own light grey
        /// around a tooltip header icon.
        /// </summary>
        internal static ItemIconFrame Explicit(Color color)
        {
            return new ItemIconFrame(color);
        }
    }
}
