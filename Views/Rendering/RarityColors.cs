using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "10./11. Coin/
    // currency value rendering primitives" and "Generic control/format
    // helpers" regions - private static -> internal static, no logic
    // changes. Callers in CraftingPlanView now qualify as
    // RarityColors.GetRarityBorderColor / RarityColors.GetRarityNameColor.
    internal static class RarityColors
    {
        /// <summary>
        /// Standard GW2 rarity palette for icon borders. Unknown/absent
        /// rarity renders a neutral dark grey - never guess a rarity.
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
                case "Fine": return new Color(98, 164, 218);
                case "Masterwork": return new Color(26, 147, 6);
                case "Rare": return new Color(252, 208, 11);
                case "Exotic": return new Color(255, 164, 5);
                case "Ascended": return new Color(251, 62, 141);
                case "Legendary": return new Color(160, 95, 240);
                default: return new Color(60, 60, 60);
            }
        }

        /// <summary>
        /// GW2's in-game-bright rarity palette for item NAME text on Blish's
        /// dark background (gw2efficiency's own name-color palette is
        /// deliberately dimmed for a white page and is illegible here).
        /// Unknown/absent rarity renders a neutral light grey - never guess.
        /// </summary>
        internal static Color GetRarityNameColor(string rarity)
        {
            switch (rarity)
            {
                case "Junk": return new Color(170, 170, 170);
                case "Basic": return new Color(255, 255, 255);
                case "Fine": return new Color(98, 164, 218);
                case "Masterwork": return new Color(26, 147, 6);
                case "Rare": return new Color(252, 208, 11);
                case "Exotic": return new Color(255, 164, 5);
                case "Ascended": return new Color(251, 62, 141);
                case "Legendary": return new Color(160, 95, 240);
                default: return new Color(200, 200, 200);
            }
        }
    }
}
