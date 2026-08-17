using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Which of the two maintainer-specified rules (docs/gw2e-considerations.md,
    /// source-selection-simplification) caused a pill to be subdued.
    /// </summary>
    public enum PillSubduingRule
    {
        None,

        /// <summary>
        /// Both options' PillSourceCostBreakdown.DecisionValue are non-null
        /// (every cost component of both is valued) and the losing option
        /// is strictly more expensive - "more expensive at your current
        /// currency values". Any strictly-positive margin counts: a pill
        /// only reaches this comparison at all when it is one of 2-3 real,
        /// offered choices (DecisionPillPlanner's own "pill count is the
        /// affordance" contract), so an objectively (if narrowly) worse
        /// valued option is still worth flagging rather than silently
        /// under-reporting it behind an arbitrary percentage threshold.
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
        StrictDomination
    }

    /// <summary>
    /// One cost kind where the losing option needed strictly more than the
    /// selected option, in StrictDomination's raw (unvalued) terms. Id is
    /// internal-only (repo invariant) - the caller resolves it to a
    /// currency/item name before ever showing it to the user; this class
    /// (and PillSubduingEvaluator) never format display text.
    /// </summary>
    public sealed class PillCostDelta
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

    public sealed class PillSubduingResult
    {
        public static readonly PillSubduingResult None = new PillSubduingResult(PillSubduingRule.None, null, null);

        public PillSubduingRule Rule { get; }

        /// <summary>Weighted only: losing.DecisionValue - selected.DecisionValue, always &gt; 0. Null for every other rule.</summary>
        public long? ValueMarginCopper { get; }

        /// <summary>StrictDomination only: every kind (Coin/Currency/Item) where losing &gt; selected. Null for every other rule.</summary>
        public IReadOnlyList<PillCostDelta> Deltas { get; }

        /// <summary>
        /// Weighted only (adversarial-review finding): true when either
        /// side's PillSourceCostBreakdown.CostLines is non-empty, i.e. a
        /// non-coin (Currency/Item) cost participates SOMEWHERE in this
        /// comparison. Weighted very commonly fires on a pure-gold
        /// difference with no currency valuation involved at all
        /// (StrictDomination cannot fire whenever the losing side's
        /// RawCoin is LOWER than the selected side's - e.g. losing craft
        /// beats selected TP on raw coin but loses on DecisionValue for an
        /// unrelated reason), so the tooltip wording must not blame "your
        /// current currency values" for a difference that never touched a
        /// currency valuation. False (the default) for every other rule.
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
    /// source-selection-simplification (maintainer-approved redesign,
    /// docs/gw2e-considerations.md): pure, Blish-free detection of whether
    /// a losing acquisition-source pill should render subdued - see
    /// PillSubduingRule's own two-case doc comments for the exact rules.
    /// Pure comparison of two PillSourceCostBreakdown values; never reads
    /// a CraftingTreeNode, never resolves a name, never decides which pill
    /// is Selected (DecisionPillPlanner's job) - the SAME "detection is a
    /// pure testable Services class" pattern CraftCompetencyEvaluator
    /// already established for the competency-aware-default rule in this
    /// same redesign.
    /// </summary>
    public static class PillSubduingEvaluator
    {
        public static PillSubduingResult Evaluate(PillSourceCostBreakdown selected, PillSourceCostBreakdown losing)
        {
            if (selected == null || losing == null || !selected.IsAvailable || !losing.IsAvailable)
            {
                return PillSubduingResult.None;
            }

            // Adversarial-review fix (Critical #5): an incomplete
            // breakdown (a real cost component with no representable
            // line - see PillSourceCostBreakdown.IsIncomplete's own doc
            // comment) cannot honestly support ANY subduing claim on
            // either side - the same conservative posture
            // VendorComponentCostsUnreliable already takes elsewhere.
            if (selected.IsIncomplete || losing.IsIncomplete)
            {
                return PillSubduingResult.None;
            }

            // Adversarial-review fix (Critical #4): a raw-quantity
            // StrictDomination claim is unreliable the moment either side
            // is a CRAFT breakdown whose ingredients were discounted by
            // owned stock the other (vendor) side never sees - see
            // PillSourceCostBreakdown.RawQuantitiesReducedByOwnedStock's
            // own doc comment. Weighted (below) is unaffected - its
            // DecisionValue figures already reflect the real, correctly-
            // discounted economics on both sides.
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
                bool hasNonCoinCost = (selected.CostLines != null && selected.CostLines.Count > 0) ||
                    (losing.CostLines != null && losing.CostLines.Count > 0);
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
