using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Resolves raw currency ids (CostLine/CurrencyCost) to display-ready
    /// name/icon (CurrencyAmountViewModel), preferring live
    /// CurrencyMetadataService data and falling back to the offline
    /// Gw2Constants table - the exact same chain PlanViewModelBuilder's
    /// Summary-section currency rows have always used, shared so
    /// shopping-row and recipe-tree currency costs never
    /// drift from it. Blish-free (plain C#, no Blish/Gw2Sharp types) so the
    /// mapping is directly unit-testable - see CurrencyDisplayResolverTests.
    /// The no-displayed-IDs invariant is enforced by construction here:
    /// CurrencyAmountViewModel has no id field at all, only Amount/Name/
    /// IconUrl, so a caller cannot accidentally surface a raw currency id.
    /// </summary>
    public static class CurrencyDisplayResolver
    {
        /// <summary>
        /// Prefers the live-fetched currency name when currencyMetadata has
        /// resolved it, falling back to the offline Gw2Constants table when
        /// metadata is null/absent for this id or came back with an empty
        /// name.
        /// </summary>
        public static string ResolveName(
            int currencyId, IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (currencyMetadata != null &&
                currencyMetadata.TryGetValue(currencyId, out var meta) &&
                !string.IsNullOrEmpty(meta.Name))
            {
                return meta.Name;
            }
            return Gw2Constants.ResolveCurrencyName(currencyId);
        }

        /// <summary>
        /// Icon for a currency amount; null (never a placeholder guess)
        /// when metadata is absent for this id or has no icon URL.
        /// </summary>
        public static string ResolveIconUrl(
            int currencyId, IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (currencyMetadata != null &&
                currencyMetadata.TryGetValue(currencyId, out var meta) &&
                !string.IsNullOrEmpty(meta.IconUrl))
            {
                return meta.IconUrl;
            }
            return null;
        }

        /// <summary>
        /// Resolves a full non-coin currency cost (CostLine list, already
        /// scaled to the caller's quantity - e.g. PlanStep/SolverDecision's
        /// VendorCurrencyCosts, or a CraftingTreeNode's) into display-ready
        /// amounts. Null/empty input yields null (never an empty-but-
        /// non-null list), so callers can use a simple null/Count==0 check
        /// for "does this row/node have a currency cost at all".
        ///
        /// ownedCurrencyAmounts (gw2e's ownedCurrencies split -
        /// optional, cosmetic only) sets each line's OwnedQuantity to
        /// min(line.Count, wallet amount) when the wallet holds any of that
        /// currency; null (not 0) when the caller has no wallet data at all
        /// or omits it, or the currency simply isn't in the wallet snapshot.
        /// The same pass also sets RawOwnedQuantity to the real, UNCLAMPED
        /// wallet amount under the identical null-vs-set conditions
        /// (shoplist-have-format) - see CurrencyAmountViewModel.
        /// RawOwnedQuantity's own doc comment for why the clamped and raw
        /// figures both need to survive to callers.
        ///
        /// Callers resolving a per-unit "Each" amount should not pass this
        /// (ownership is a total-quantity concept - see ResolveUnitAmounts,
        /// which never accepts it).
        /// </summary>
        public static List<CurrencyAmountViewModel> ResolveAmounts(
            IReadOnlyList<CostLine> costLines,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts = null)
        {
            if (costLines == null || costLines.Count == 0)
            {
                return null;
            }

            var result = new List<CurrencyAmountViewModel>(costLines.Count);
            foreach (var line in costLines)
            {
                int? owned = null;
                int? rawOwned = null;
                if (ownedCurrencyAmounts != null &&
                    ownedCurrencyAmounts.TryGetValue(line.Id, out int ownedRaw))
                {
                    owned = Math.Min(ownedRaw, line.Count);
                    rawOwned = ownedRaw;
                }

                result.Add(new CurrencyAmountViewModel
                {
                    Amount = line.Count,
                    Name = ResolveName(line.Id, currencyMetadata),
                    IconUrl = ResolveIconUrl(line.Id, currencyMetadata),
                    OwnedQuantity = owned,
                    RawOwnedQuantity = rawOwned
                });
            }
            return result;
        }

        /// <summary>
        /// Per-unit ("Each") counterpart of ResolveAmounts for a vendor-
        /// priced currency cost - the WINNING OFFER's true per-unit rate
        /// (its own per-batch cost line divided by its own OutputCount), not
        /// a truncated average over the row's aggregated total/Quantity
        ///. The previous total/quantity truncating-average
        /// approach could show a misleading "1" for a merged row whose real
        /// purchases were e.g. 3-for-3 plus 1-for-1 batches; this resolves
        /// the actual offer rate instead.
        ///
        /// perBatchCostLines/outputCount come from PlanStep.
        /// VendorOfferCurrencyCostLinesPerBatch/VendorOfferOutputCount,
        /// which are only populated when every tree occurrence merged into
        /// that step used the identical winning offer (see
        /// VendorBatchSolver.FinalizeVendorBatches) - null/0 otherwise (mixed
        /// offers, or a non-vendor row), in which case this returns null
        /// rather than reviving the old misleading average: gw2efficiency
        /// itself never shows a per-unit currency price at all (docs/
        /// gw2e-parity-spec.md Section 4.3/directive 5), so omitting the
        /// Each cell is the closer parity choice than guessing.
        ///
        /// When a line's per-batch count does not divide evenly by
        /// outputCount, the true rate is not a whole number; rather than
        /// round (inventing data the spec doesn't ask for), the amount
        /// carries a literal "N for M" bundle label instead (see
        /// CurrencyAmountViewModel.BundleLabel) for the caller to render as
        /// text.
        /// </summary>
        public static List<CurrencyAmountViewModel> ResolveUnitAmounts(
            int outputCount,
            IReadOnlyList<CostLine> perBatchCostLines,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            return ResolveDividedAmounts(perBatchCostLines, outputCount, currencyMetadata);
        }

        /// <summary>
        /// Approximates a per-unit ("Each") currency amount for a single
        /// recipe-tree row (TreeSectionController's "Unit price:" tooltip
        /// line, field-test finding B) from a node's own already-scaled-to-
        /// Quantity VendorCurrencyCosts. Unlike ResolveUnitAmounts, this is
        /// NOT the winning offer's true per-batch rate: CraftingTreeNode
        /// carries no per-offer batch data (OutputCount/
        /// CurrencyCostLinesPerBatch only exist on PlanStep, threaded there
        /// by VendorBatchSolver.FinalizeVendorBatches for the MERGED
        /// shopping list - a later, separate pass a single tree node's
        /// SolverDecision never goes through), so this divides the node's
        /// own total by its own Quantity instead. The two happen to agree
        /// whenever this offer's purchase batches divided evenly into this
        /// node's Quantity (the common case); when they do not (the total
        /// already includes rounding up to a whole purchase - see
        /// VendorBatchSolver.EvaluateVendorOffers' unitsNeeded), this falls
        /// back to the same "N for M" bundle text as ResolveUnitAmounts
        /// rather than dividing into a misleading fractional number - see
        /// that method's own doc comment for why a truncated average is
        /// avoided in the first place. Display-layer-only: no solver change
        /// needed to plumb the true batch rate down to this node.
        /// </summary>
        public static List<CurrencyAmountViewModel> ResolveTreeNodeUnitAmounts(
            IReadOnlyList<CostLine> totalCostLines,
            int quantity,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            return ResolveDividedAmounts(totalCostLines, quantity, currencyMetadata);
        }

        /// <summary>
        /// Shared "N for M" divide-with-bundle-fallback arithmetic behind
        /// both ResolveUnitAmounts (true per-batch rate) and
        /// ResolveTreeNodeUnitAmounts (total/quantity approximation) - the
        /// two callers differ only in what costLines/divisor semantically
        /// represent, never in the math itself.
        /// </summary>
        private static List<CurrencyAmountViewModel> ResolveDividedAmounts(
            IReadOnlyList<CostLine> costLines,
            int divisor,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (costLines == null || costLines.Count == 0 || divisor <= 0)
            {
                return null;
            }

            var result = new List<CurrencyAmountViewModel>(costLines.Count);
            foreach (var line in costLines)
            {
                bool evenly = line.Count % divisor == 0;
                result.Add(new CurrencyAmountViewModel
                {
                    Amount = evenly ? line.Count / divisor : 0,
                    BundleLabel = evenly ? null : $"{line.Count} for {divisor}",
                    Name = ResolveName(line.Id, currencyMetadata),
                    IconUrl = ResolveIconUrl(line.Id, currencyMetadata)
                });
            }
            return result;
        }
    }
}
