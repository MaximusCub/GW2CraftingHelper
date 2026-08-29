using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The attribute arithmetic behind a stat-selectable item's numbers, and
    /// the API-token to in-game-name map every attribute line needs.
    /// <para>
    /// THE FORMULA IS THE API'S OWN, not a reconstruction: for every
    /// fixed-stat item the endpoint publishes both the inputs
    /// (details.attribute_adjustment plus the multipliers on its
    /// /v2/itemstats entry) and the answer (infix_upgrade.attributes), and
    /// round(multiplier * attribute_adjustment) reproduces that published
    /// answer exactly. ItemStatMathTests asserts against those published
    /// answers rather than against this method's own arithmetic.
    /// </para>
    /// <para>
    /// The legacy "value" field on a /v2/itemstats attribute is NOT part of
    /// this: entry 161 reports value 0 on all three attributes while a
    /// different entry of the same name reports non-zero ones. Multiplier
    /// only. Measurements: docs/ARCHITECTURE.md section S1.4.
    /// </para>
    /// </summary>
    internal static class ItemStatMath
    {
        public static int AttributeValue(double multiplier, double attributeAdjustment)
        {
            // AwayFromZero, not banker's rounding: 0.25 * 134.442 = 33.61
            // and 0.25 * 179.256 = 44.814 both land where either mode
            // agrees, but a .5 case must round up to match the API's own
            // published modifiers rather than to the nearest even.
            return (int)Math.Round(multiplier * attributeAdjustment, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// The in-game name for a /v2/items or /v2/itemstats attribute
        /// token. An unrecognised token is returned UNCHANGED - a new
        /// attribute added by a future game build renders as its raw name
        /// rather than being dropped or guessed at.
        /// </summary>
        public static string AttributeDisplayName(string apiName)
        {
            switch (apiName)
            {
                case "CritDamage": return "Ferocity";
                case "Healing": return "Healing Power";
                case "BoonDuration": return "Concentration";
                case "ConditionDuration": return "Expertise";
                case "ConditionDamage": return "Condition Damage";
                case "AgonyResistance": return "Agony Resistance";
                case "Power": return "Power";
                case "Precision": return "Precision";
                case "Toughness": return "Toughness";
                case "Vitality": return "Vitality";
                default: return apiName ?? "";
            }
        }
    }
}
