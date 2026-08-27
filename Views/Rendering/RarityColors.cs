using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
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
}
