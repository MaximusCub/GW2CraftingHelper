using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// User-provided coin valuations for non-coin currencies, plus which
    /// currencies the user has explicitly CLEARED of the curated
    /// defaults. Precedence per currency id: user-set value wins; else a
    /// cleared currency has no value at all (a deliberate, persisted
    /// suppression); else the curated default applies. See
    /// TryGetEffectiveCopperValue for the implementation - it is not
    /// called by the solver at runtime; WithDefaults materializes the
    /// precedence into a plain dictionary first. TryGetCopperValue stays
    /// a raw user-override lookup on this instance.
    ///
    /// The GW2 API defines no exchange rate for these currencies, so the
    /// solver never invents one (repo invariant): only a currency with an
    /// effective value is usable for comparison; everything else stays
    /// unvalued and fallback-tier only.
    /// </summary>
    public class CurrencyValuation
    {
        /// <summary>
        /// No user-provided valuations or clears. "None" describes zero
        /// PERSISTED overrides/clears, not zero effective values -
        /// TryGetEffectiveCopperValue on this instance still falls through
        /// to the curated defaults; use the raw TryGetCopperValue for
        /// "truly nothing valued".
        /// </summary>
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
        /// Resolves <paramref name="currencyId"/>'s EFFECTIVE
        /// decision-only copper value per the three-state precedence
        /// documented on this class. Not called by any solver comparison
        /// at runtime - its one production caller is WithDefaults, which
        /// materializes the precedence into a plain dictionary before the
        /// solver runs. Trap for a future caller: handing Solve a raw,
        /// non-materialized CurrencyValuation silently yields zero
        /// curated defaults. Strictly DECISION-ONLY.
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
        /// Blish-free merge that turns persisted state + curated defaults
        /// into the CurrencyValuation the solver actually receives. No id
        /// can land in both the merged value set and ClearedCurrencyIds
        /// (the constructor would throw); that holds by construction
        /// because TryGetEffectiveCopperValue never returns true for a
        /// cleared id. Returns user overrides plus every non-overridden,
        /// non-cleared curated default; ClearedCurrencyIds passes through.
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
