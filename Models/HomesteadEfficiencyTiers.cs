using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// User-configured Homestead Refinement efficiency tier per material
    /// family. Echoes gw2efficiency's
    /// own cheapestTree.ts userEfficiencyTiers exactly: a per-output-material
    /// integer tier 0/1/2, defaulting to 0 (no upgrade) for every material
    /// with no entry - matching gw2e's own hardcoded default AND its
    /// no-API-key fallback (docs/research/m37-r1-homestead.md Section 1.2/1.3).
    /// Deliberately has NO master "do you even own Homestead" gate - gw2e
    /// has none either (Section 1.5); see KNOWN-ISSUES #24 for the
    /// deferred divergence option.
    /// </summary>
    public class HomesteadEfficiencyTiers
    {
        /// <summary>Tier 0 for every material. The default when nothing is configured.</summary>
        public static readonly HomesteadEfficiencyTiers Default =
            new HomesteadEfficiencyTiers(new Dictionary<int, int>());

        private readonly IReadOnlyDictionary<int, int> _tierByMaterialId;

        public HomesteadEfficiencyTiers(IReadOnlyDictionary<int, int> tierByMaterialId)
        {
            if (tierByMaterialId == null)
            {
                _tierByMaterialId = new Dictionary<int, int>();
                return;
            }

            // Defensively copied and validated, same posture as
            // CurrencyValuation: an instance is stored long-term on
            // PlanSolveContext, so a caller mutating the dictionary it
            // passed in must never retroactively change an already-built
            // configuration. A caller with a possibly-invalid map (e.g. one
            // fresh from persisted settings) must pre-filter via
            // ModuleSettings.GetHomesteadEfficiencyTiers - this constructor
            // fails loudly rather than silently clamping.
            var validated = new Dictionary<int, int>(tierByMaterialId.Count);
            foreach (var kvp in tierByMaterialId)
            {
                bool isKnownMaterial = false;
                foreach (int id in Gw2Constants.HomesteadRefinementMaterialIds)
                {
                    if (kvp.Key == id)
                    {
                        isKnownMaterial = true;
                        break;
                    }
                }

                if (!isKnownMaterial)
                {
                    throw new ArgumentException(
                        $"Item id {kvp.Key} is not a known Homestead Refinement material.",
                        nameof(tierByMaterialId));
                }

                if (kvp.Value < 0 || kvp.Value > 2)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(tierByMaterialId),
                        kvp.Value,
                        "Homestead efficiency tier must be 0, 1, or 2.");
                }

                validated[kvp.Key] = kvp.Value;
            }

            _tierByMaterialId = validated;
        }

        /// <summary>Material item id -> configured tier (0/1/2). Only ever contains known material ids.</summary>
        public IReadOnlyDictionary<int, int> TierByMaterialId => _tierByMaterialId;

        /// <summary>
        /// The configured tier for <paramref name="materialItemId"/>, or 0
        /// (gw2e's own default) when unconfigured or the id is not a known
        /// Homestead Refinement material.
        /// </summary>
        public int GetTier(int materialItemId)
        {
            return _tierByMaterialId.TryGetValue(materialItemId, out int tier) ? tier : 0;
        }
    }
}
