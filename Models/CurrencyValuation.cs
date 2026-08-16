using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// User-provided coin valuations for non-coin currencies (karma,
    /// laurels, Spirit Shards, ...), plus (currency-ux-package, Feature 1)
    /// which currencies the user has explicitly CLEARED of
    /// CurrencyDecisionDefaults' curated defaults. Three effective states
    /// per currency id, in precedence order: (1) an explicit user-set value
    /// here always wins; (2) failing that, a currency in ClearedCurrencyIds
    /// has no value at all, even if CurrencyDecisionDefaults has one -
    /// "cleared" is a deliberate, persisted suppression, not merely "no
    /// override yet"; (3) failing that, CurrencyDecisionDefaults' curated
    /// value applies, if it has one. See TryGetEffectiveCopperValue, the
    /// solver's own entry point for this precedence - TryGetCopperValue
    /// below stays a RAW lookup of state (1) only (user overrides), used by
    /// the Settings tab to know what to show in a currency's text box.
    ///
    /// The GW2 API defines no exchange rate for these currencies at all, so
    /// the solver never invents one on its own (repo invariant): only a
    /// currency with an effective value (user-set, or a non-cleared
    /// default) is usable for cost comparison. Every other currency remains
    /// unvalued, and vendor offers/recipes charging them stay fallback-tier
    /// only (see VendorBatchSolver.EvaluateVendorOffers).
    /// </summary>
    public class CurrencyValuation
    {
        /// <summary>No user-provided valuations or clears. The default when none is configured.</summary>
        public static readonly CurrencyValuation None = new CurrencyValuation(new Dictionary<int, long>());

        private readonly IReadOnlyDictionary<int, long> _copperPerUnit;
        private readonly HashSet<int> _clearedCurrencyIds;

        public CurrencyValuation(IReadOnlyDictionary<int, long> copperPerUnit, IEnumerable<int> clearedCurrencyIds = null)
        {
            var validated = new Dictionary<int, long>();
            if (copperPerUnit != null)
            {
                // Defensively copied: instances are stored long-term on
                // PlanSolveContext, so a caller mutating the dictionary it
                // passed in must never retroactively change an already-built
                // valuation. Validated while copying: an invalid entry here
                // would either be inert (<=0 copper never beats a coin
                // option) or nonsensical (coin priced in terms of itself),
                // so callers must fix the input rather than have it
                // silently accepted.
                foreach (var kvp in copperPerUnit)
                {
                    if (kvp.Key == Gw2Constants.CoinCurrencyId)
                    {
                        throw new ArgumentException(
                            "Currency valuation cannot be keyed on the coin currency id.",
                            nameof(copperPerUnit));
                    }
                    if (kvp.Value <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(copperPerUnit),
                            kvp.Value,
                            $"Currency {kvp.Key} must have a positive copper-per-unit valuation.");
                    }
                    validated[kvp.Key] = kvp.Value;
                }
            }
            _copperPerUnit = validated;

            var validatedCleared = new HashSet<int>();
            if (clearedCurrencyIds != null)
            {
                // Same fail-loud-for-direct-construction posture as the
                // values dictionary above (mirrors HomesteadEfficiencyTiers'
                // own constructor/ModuleSettings-clamp split - see that
                // class's doc comment): a caller passing a currency id that
                // is BOTH explicitly valued and marked cleared is
                // self-contradictory input, not something this constructor
                // should silently resolve one way or the other. Callers
                // that build both sets from a shared, possibly-overlapping
                // source (CurrencyValuationSerializer.Deserialize,
                // SettingsTabContent.SaveValuations) resolve the conflict
                // themselves (explicit value always wins) before ever
                // reaching here.
                foreach (int currencyId in clearedCurrencyIds)
                {
                    if (currencyId == Gw2Constants.CoinCurrencyId)
                    {
                        throw new ArgumentException(
                            "Currency valuation cannot clear the coin currency id.",
                            nameof(clearedCurrencyIds));
                    }
                    if (validated.ContainsKey(currencyId))
                    {
                        throw new ArgumentException(
                            $"Currency {currencyId} cannot be both explicitly valued and cleared.",
                            nameof(clearedCurrencyIds));
                    }
                    validatedCleared.Add(currencyId);
                }
            }
            _clearedCurrencyIds = validatedCleared;
        }

        /// <summary>CurrencyId -> copper value of a single unit of that currency (user overrides only).</summary>
        public IReadOnlyDictionary<int, long> CopperPerUnit => _copperPerUnit;

        /// <summary>
        /// Currency ids the user has explicitly cleared of
        /// CurrencyDecisionDefaults' curated default - deliberately
        /// suppressed, not merely "no override set" (see class doc comment).
        /// </summary>
        public IReadOnlyCollection<int> ClearedCurrencyIds => _clearedCurrencyIds;

        /// <summary>
        /// Looks up the user-provided copper value of one unit of
        /// <paramref name="currencyId"/>. Returns false when the user has
        /// not set an explicit override for that currency - this is a RAW
        /// lookup that never consults CurrencyDecisionDefaults; see
        /// TryGetEffectiveCopperValue for the solver's own value, which
        /// does.
        /// </summary>
        public bool TryGetCopperValue(int currencyId, out long copperPerUnit)
        {
            return _copperPerUnit.TryGetValue(currencyId, out copperPerUnit);
        }

        /// <summary>
        /// True when the user has explicitly cleared
        /// <paramref name="currencyId"/> of its curated default.
        /// </summary>
        public bool IsCleared(int currencyId)
        {
            return _clearedCurrencyIds.Contains(currencyId);
        }

        /// <summary>
        /// The solver's own entry point (currency-ux-package, Feature 1):
        /// resolves <paramref name="currencyId"/>'s EFFECTIVE decision-only
        /// copper value per the three-state precedence documented on this
        /// class - user override, else (if not cleared) CurrencyDecisionDefaults'
        /// curated default, else no value at all. Still strictly
        /// DECISION-ONLY (repo invariant, restated here since this is the
        /// one method every currency-cost comparison in the solver actually
        /// calls) - the returned value may tip a craft-vs-buy/vendor-vs-TP
        /// comparison, but must never be folded into a displayed gold total.
        /// </summary>
        public bool TryGetEffectiveCopperValue(int currencyId, out long copperPerUnit)
        {
            if (TryGetCopperValue(currencyId, out copperPerUnit))
            {
                return true;
            }
            if (_clearedCurrencyIds.Contains(currencyId))
            {
                copperPerUnit = 0;
                return false;
            }
            return CurrencyDecisionDefaults.TryGetDefault(currencyId, out copperPerUnit);
        }

        /// <summary>
        /// currency-ux-package review fix (finding 5, MEASURED): the
        /// Blish-free counterpart of the merge previously inlined directly
        /// inside ModuleSettings.GetEffectiveCurrencyValuation - that class
        /// is Blish-coupled and therefore untestable per repo invariant,
        /// which left the merge that turns persisted state + defaults into
        /// the CurrencyValuation the solver actually receives completely
        /// unverified, including the non-obvious invariant this method
        /// depends on (no id can land in both the merged value set and
        /// ClearedCurrencyIds, which would throw from this class's own
        /// constructor above - see TryGetEffectiveCopperValue, which never
        /// returns true for an id while IsCleared(id) is also true, so that
        /// invariant holds by construction here). Same split this repo
        /// already uses for CurrencyValuationSerializer.
        ///
        /// Returns a new CurrencyValuation containing every entry
        /// EFFECTIVELY visible to the solver from <paramref name="persisted"/>
        /// - user overrides plus, for every id in
        /// CurrencyDecisionDefaults.DefaultCopperPerUnit that is neither
        /// overridden nor cleared, that curated default value.
        /// ClearedCurrencyIds passes through unchanged.
        /// </summary>
        public static CurrencyValuation WithDefaults(CurrencyValuation persisted)
        {
            persisted = persisted ?? None;

            var candidateIds = new HashSet<int>(persisted.CopperPerUnit.Keys);
            foreach (int currencyId in CurrencyDecisionDefaults.DefaultCopperPerUnit.Keys)
            {
                candidateIds.Add(currencyId);
            }

            var merged = new Dictionary<int, long>();
            foreach (int currencyId in candidateIds)
            {
                if (persisted.TryGetEffectiveCopperValue(currencyId, out long copperPerUnit))
                {
                    merged[currencyId] = copperPerUnit;
                }
            }

            return new CurrencyValuation(merged, persisted.ClearedCurrencyIds);
        }
    }
}
