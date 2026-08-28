using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Which of the two rules caused a pill to be subdued.
    /// </summary>
    internal enum PillSubduingRule
    {
        None,

        /// <summary>
        /// Both options' DecisionValue are non-null and the losing option
        /// is strictly more expensive by a DECISIVE margin (see
        /// IsDecisiveMargin) - a bare positive margin is not enough; a
        /// 1-copper difference on a multi-gold purchase must not render
        /// as "more expensive".
        /// </summary>
        Weighted,

        /// <summary>
        /// The losing option's RawCoin and every CostLine kind are each
        /// greater than or equal to the selected option's (missing kinds
        /// on either side treated as zero), with at least one strictly
        /// greater - "always more expensive - needs everything the
        /// selected option needs, plus N more X".
        /// Needs no valuation at all: this is a fact about raw quantities,
        /// true regardless of the user's currency values (covers e.g.
        /// Amalgamated Rift Essence: a vendor trade-in needing the SAME
        /// base cost plus 10 more Globs of Ectoplasm than crafting does).
        /// </summary>
        StrictDomination,
    }

    /// <summary>
    /// One cost kind where the losing option needed strictly more than the
    /// selected option, in StrictDomination's raw (unvalued) terms. Id is
    /// internal-only (repo invariant) - the caller resolves it to a
    /// currency/item name before ever showing it to the user; this class
    /// (and PillSubduingEvaluator) never format display text.
    /// </summary>
    internal sealed class PillCostDelta
    {
        public string Kind { get; }

        public int Id { get; }

        public long Amount { get; }

        public PillCostDelta(string kind, int id, long amount)
        {
            Kind = kind;
            Id = id;
            Amount = amount;
        }
    }

    internal sealed class PillSubduingResult
    {
        public static readonly PillSubduingResult None = new PillSubduingResult(PillSubduingRule.None, null, null);

        public PillSubduingRule Rule { get; }

        /// <summary>Weighted only: losing.DecisionValue - selected.DecisionValue, always &gt; 0. Null for every other rule.</summary>
        public long? ValueMarginCopper { get; }

        /// <summary>StrictDomination only: every kind (Coin/Currency/Item) where losing &gt; selected. Null for every other rule.</summary>
        public IReadOnlyList<PillCostDelta> Deltas { get; }

        /// <summary>
        /// Weighted only: true when either side has a Type == "Currency"
        /// cost line - the only kind a CurrencyValuation ever prices.
        /// Type == "Item" lines do NOT count (they are TP-priced, never
        /// user-valued), so the tooltip never blames "your current
        /// currency values" for a plain-gold difference. False for every
        /// other rule.
        /// </summary>
        public bool HasNonCoinCost { get; }

        public PillSubduingResult(
            PillSubduingRule rule, long? valueMarginCopper, IReadOnlyList<PillCostDelta> deltas,
            bool hasNonCoinCost = false)
        {
            Rule = rule;
            ValueMarginCopper = valueMarginCopper;
            Deltas = deltas;
            HasNonCoinCost = hasNonCoinCost;
        }
    }

    /// <summary>
    /// Pure, Blish-free detection of whether a losing acquisition-source
    /// pill should render subdued (see PillSubduingRule for the two
    /// rules). Compares two PillSourceCostBreakdown values only; never
    /// reads a CraftingTreeNode, resolves a name, or decides which pill
    /// is Selected.
    /// </summary>
    internal static class PillSubduingEvaluator
    {
        // "Decisive" requires BOTH an absolute floor (non-trivial coin
        // even for a cheap item) AND a relative floor (non-trivial
        // percentage even for an expensive one) - requiring both is the
        // conservative reading. The constants are a modest, tunable
        // starting point, not maintainer-derived figures.
        private const long MinDecisiveMarginCopper = 100; // 1 silver
        private const double MinDecisiveMarginFraction = 0.01; // 1%

        /// <summary>
        /// True when <paramref name="marginCopper"/> (always &gt; 0 at the
        /// call site) is large enough, both in absolute copper and as a
        /// fraction of <paramref name="selectedValueCopper"/>, to call the
        /// losing option "more expensive" rather than "near-equal". See
        /// this class's MinDecisiveMarginCopper/MinDecisiveMarginFraction
        /// fields for the exact floors and their rationale.
        /// </summary>
        private static bool IsDecisiveMargin(long marginCopper, long selectedValueCopper)
        {
            if (marginCopper < MinDecisiveMarginCopper)
            {
                return false;
            }

            // selectedValueCopper is always >= 0 (RawCoin/DecisionValue are
            // never negative); a selected value of exactly 0 (e.g. a
            // free/fully-owned source) makes ANY strictly-positive margin
            // an infinite relative jump - the absolute floor above already
            // gates that case, so the relative floor is skipped rather
            // than dividing by zero.
            if (selectedValueCopper <= 0)
            {
                return true;
            }

            return marginCopper >= selectedValueCopper * MinDecisiveMarginFraction;
        }

        public static PillSubduingResult Evaluate(PillSourceCostBreakdown selected, PillSourceCostBreakdown losing)
        {
            if (selected == null || losing == null || !selected.IsAvailable || !losing.IsAvailable)
            {
                return PillSubduingResult.None;
            }

            // An incomplete breakdown (a real cost component with no
            // representable line) cannot honestly support any subduing
            // claim on either side.
            if (selected.IsIncomplete || losing.IsIncomplete)
            {
                return PillSubduingResult.None;
            }

            // A raw-quantity StrictDomination claim is unreliable when
            // either side's craft ingredients were discounted by owned
            // stock the other side never sees. Weighted is unaffected -
            // its DecisionValue already reflects the discounted economics.
            if (!selected.RawQuantitiesReducedByOwnedStock && !losing.RawQuantitiesReducedByOwnedStock)
            {
                // STRICT DOMINATION checked first - a stronger, valuation-
                // free claim, preferred over WEIGHTED whenever both would
                // apply.
                var deltas = TryComputeDomination(selected, losing);
                if (deltas != null)
                {
                    return new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);
                }
            }

            if (selected.DecisionValue.HasValue && losing.DecisionValue.HasValue &&
                losing.DecisionValue.Value > selected.DecisionValue.Value)
            {
                long margin = losing.DecisionValue.Value - selected.DecisionValue.Value;

                // A near-equal losing option that fails the decisive
                // floor stays None, same as a genuine tie.
                if (!IsDecisiveMargin(margin, selected.DecisionValue.Value))
                {
                    return PillSubduingResult.None;
                }

                bool hasNonCoinCost = HasCurrencyLine(selected.CostLines) || HasCurrencyLine(losing.CostLines);
                return new PillSubduingResult(PillSubduingRule.Weighted, margin, null, hasNonCoinCost);
            }

            return PillSubduingResult.None;
        }

        /// <summary>
        /// Null when NOT dominated (some kind favors the losing option, or
        /// every kind ties exactly - domination requires a strict
        /// inequality on at least one kind, not a tie everywhere).
        /// Otherwise the list of every kind where losing &gt; selected
        /// (always non-empty when non-null).
        /// </summary>
        private static List<PillCostDelta> TryComputeDomination(
            PillSourceCostBreakdown selected, PillSourceCostBreakdown losing)
        {
            var deltas = new List<PillCostDelta>();

            long coinDelta = losing.RawCoin - selected.RawCoin;
            if (coinDelta < 0)
            {
                return null;
            }

            if (coinDelta > 0)
            {
                deltas.Add(new PillCostDelta("Coin", 0, coinDelta));
            }

            // Union of every (Type, Id) kind present on either side -
            // absent on one side reads as 0 there (see PillSubduingRule.
            // StrictDomination's own doc comment for why that is the only
            // sensible reading: a cost line is never emitted at Count 0).
            var selectedByKind = ToLookup(selected.CostLines);
            var losingByKind = ToLookup(losing.CostLines);
            var allKinds = new HashSet<(string Type, int Id)>(selectedByKind.Keys);
            allKinds.UnionWith(losingByKind.Keys);

            foreach (var kind in allKinds)
            {
                int selectedAmount = selectedByKind.TryGetValue(kind, out int sAmt) ? sAmt : 0;
                int losingAmount = losingByKind.TryGetValue(kind, out int lAmt) ? lAmt : 0;
                long delta = losingAmount - selectedAmount;
                if (delta < 0)
                {
                    return null;
                }

                if (delta > 0)
                {
                    deltas.Add(new PillCostDelta(kind.Type, kind.Id, delta));
                }
            }

            return deltas.Count > 0 ? deltas : null;
        }

        /// <summary>
        /// True when at least one line is Type == "Currency" - the only
        /// CostLine kind a CurrencyValuation ever prices (see
        /// HasNonCoinCost's own doc comment). Type == "Item" lines are
        /// TP-priced and never count, regardless of how many are present.
        /// </summary>
        private static bool HasCurrencyLine(IReadOnlyList<CostLine> lines)
        {
            if (lines == null)
            {
                return false;
            }

            foreach (var line in lines)
            {
                if (line.Type == "Currency")
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<(string Type, int Id), int> ToLookup(IReadOnlyList<CostLine> lines)
        {
            var lookup = new Dictionary<(string, int), int>();
            if (lines == null)
            {
                return lookup;
            }

            foreach (var line in lines)
            {
                var key = (line.Type, line.Id);
                lookup[key] = lookup.TryGetValue(key, out int existing) ? existing + line.Count : line.Count;
            }

            return lookup;
        }
    }
}
