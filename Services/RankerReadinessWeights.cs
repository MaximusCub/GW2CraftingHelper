using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The Crafting Ranker's headline is a weighted mean of four gate
    /// completions, renormalised over the gates that apply to the item. These
    /// are the weights, kept as named constants rather than buried in the
    /// formula so they can be argued with.
    ///
    /// They are NOT derived from each gate's magnitude. Deriving them that way
    /// sounds principled and is the exchange-rate trap in disguise: to weight
    /// days against coin by magnitude you must first decide what a day is
    /// worth in gold, and neither the GW2 API nor this repo will supply that
    /// number. They are judgement calls about SUBSTITUTABILITY instead, which
    /// is a property the game itself decides:
    ///
    ///  - A daily reset cannot be bought at any price. It is the only barrier
    ///    with no substitute, so it takes the largest share.
    ///  - Coin is the bulk of the work and the one gate measured exactly, by
    ///    the real solver at real prices. Equal claim on precision grounds; no
    ///    better claim than time on difficulty grounds.
    ///  - Currencies are a real barrier measured only as within-currency
    ///    ratios, so each point carries less information than a coin point.
    ///    Weighted below materials for that reason, not because currencies
    ///    matter less.
    ///  - A discipline is a hard wall - you cannot craft at all without it -
    ///    but a short one next to a legendary's materials bill, and usually
    ///    either satisfied already or cheap to satisfy. Non-zero because it is
    ///    real; small because it is short.
    ///
    /// Deliberately not a user setting: a user who retunes the weights cannot
    /// compare their own numbers with anyone else's, and the model's
    /// legibility is the feature.
    /// </summary>
    public static class RankerReadinessWeights
    {
        public const double TimeGates = 0.35;
        public const double Materials = 0.35;
        public const double Currencies = 0.20;
        public const double Disciplines = 0.10;

        public static double For(RankerGate gate)
        {
            switch (gate)
            {
                case RankerGate.TimeGates: return TimeGates;
                case RankerGate.Materials: return Materials;
                case RankerGate.Currencies: return Currencies;
                case RankerGate.Disciplines: return Disciplines;
                default: return 0;
            }
        }
    }
}
