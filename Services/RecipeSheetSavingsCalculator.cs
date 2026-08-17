using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// opportunity-notes (RECIPE-SHEET SAVINGS): pure, Blish-free post-solve
    /// annotation pass, same architectural role/placement precedent as
    /// ExcessCraftOutputCalculator - walks the already-built display tree
    /// (CraftingTree/MultiItemRoots) looking for a BOUGHT item (Decision !=
    /// Craft) whose reference branch (CraftingTreeBuilder's "what it would
    /// cost to craft instead") is blocked on an unlearned, LearnedFromItem
    /// recipe that a purchasable recipe sheet would unlock - and would be
    /// cheaper to craft than to keep buying.
    ///
    /// Data sources, all pre-existing (repo invariant: never invent data):
    /// - CraftingTreeNode.ReferenceRecipeId/Disciplines/MinRating/
    ///   IsLearnedFromItem (CraftingTreeBuilder, this same feature).
    /// - learnedRecipeIds (account recipe permission - IAccountRecipeClient,
    ///   same source RequiredRecipe.IsMissing already uses).
    /// - characterDisciplines (account snapshot - same source
    ///   RequiredDisciplines' own competency notes already use).
    /// - recipeSheetItemIdByRecipeId: the ONE genuinely new piece of data
    ///   this feature needs (no GW2 API endpoint or existing seed maps a
    ///   recipe id to its unlocking item id - see this module's own
    ///   docs/KNOWN-ISSUES.md entry for this feature). Deliberately NOT a
    ///   reverse-index/discovery pipeline (maintainer direction: "no
    ///   reverse-sheet-index plumbing") - just an injectable, optional
    ///   lookup a caller may populate with wiki-verified entries over time;
    ///   empty by default (CraftingPlanPipeline's own constructor default),
    ///   so this calculator emits nothing today for any recipe not yet in
    ///   that curated map, exactly matching "where the sheet is not in our
    ///   data, emit nothing".
    ///
    /// Writes only CraftingPlanResult.RecipeSheetSavingsOpportunities.
    /// Never mutates Plan/any total - same "advisory, never fed back into a
    /// decision" contract as ExcessCraftOutputCalculator's own doc comment.
    /// </summary>
    internal static class RecipeSheetSavingsCalculator
    {
        // B8 shape fix: narrowed from the full VendorOfferStore to the one
        // method this calculator ever calls on it (GetOffersForItem) - a
        // plain Func<int, IReadOnlyList<VendorOffer>> is both a smaller
        // surface (this class cannot reach any other VendorOfferStore
        // member, e.g. LoadBaseline/AddOffersToOverlay) and lets a caller
        // (or test) hand in ANY offer source, not only a real store
        // instance. The null-delegate guard below replaces the old
        // `vendorOfferStore != null` check with the exact same meaning:
        // "no offer source available -> emit nothing".
        internal static void Apply(
            CraftingPlanResult result,
            ISet<int> learnedRecipeIds,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis,
            Func<int, IReadOnlyList<VendorOffer>> offersForItem,
            IReadOnlyDictionary<int, int> recipeSheetItemIdByRecipeId,
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines)
        {
            var opportunities = new List<RecipeSheetSavingsOpportunity>();

            if (result == null)
            {
                return;
            }

            // Every one of these is required to safely determine "missing"
            // (learnedRecipeIds), "purchasable" (offersForItem/prices), or
            // "worth the note" (recipeSheetItemIdByRecipeId non-empty) - a
            // missing input means the answer is genuinely unknown, so no
            // opportunity is ever fabricated. Mirrors BuildNotesSection's
            // own "characterDisciplines == null -> never renders a
            // competency line" null-safety contract.
            if (learnedRecipeIds != null && prices != null && offersForItem != null &&
                recipeSheetItemIdByRecipeId != null && recipeSheetItemIdByRecipeId.Count > 0)
            {
                var seenItemIds = new HashSet<int>();

                if (result.CraftingTree != null)
                {
                    Walk(result.CraftingTree, learnedRecipeIds, prices, priceBasis, offersForItem,
                        recipeSheetItemIdByRecipeId, characterDisciplines, seenItemIds, opportunities);
                }

                if (result.MultiItemRoots != null)
                {
                    foreach (var root in result.MultiItemRoots)
                    {
                        Walk(root, learnedRecipeIds, prices, priceBasis, offersForItem,
                            recipeSheetItemIdByRecipeId, characterDisciplines, seenItemIds, opportunities);
                    }
                }
            }

            result.RecipeSheetSavingsOpportunities = opportunities;
        }

        private static void Walk(
            CraftingTreeNode node,
            ISet<int> learnedRecipeIds,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis,
            Func<int, IReadOnlyList<VendorOffer>> offersForItem,
            IReadOnlyDictionary<int, int> recipeSheetItemIdByRecipeId,
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines,
            HashSet<int> seenItemIds,
            List<RecipeSheetSavingsOpportunity> opportunities)
        {
            if (node == null)
            {
                return;
            }

            if (node.IsReferenceBranch &&
                node.ReferenceRecipeId.HasValue &&
                node.ReferenceRecipeIsLearnedFromItem &&
                learnedRecipeIds.Contains(node.ReferenceRecipeId.Value) == false &&
                node.Quantity > 0 &&
                node.UnitCost.HasValue &&
                // A vendor decision priced partly in non-coin currency is
                // not fully represented by UnitCost alone (see
                // PlanStep.VendorCurrencyCosts' own doc comment) - not
                // safely comparable to a pure-coin craft cost.
                (node.VendorCurrencyCosts == null || node.VendorCurrencyCosts.Count == 0) &&
                // Read-only check here (Add happens only on a successful
                // emit, inside TryEmit) - an earlier tree occurrence of
                // this same item that failed to qualify (e.g. a different
                // owned/reduction context made its own reference branch
                // unprovable) must not block a LATER occurrence that does
                // qualify.
                !seenItemIds.Contains(node.ItemId) &&
                recipeSheetItemIdByRecipeId.TryGetValue(node.ReferenceRecipeId.Value, out int sheetItemId))
            {
                TryEmit(node, sheetItemId, prices, priceBasis, offersForItem, characterDisciplines,
                    seenItemIds, opportunities);
            }

            foreach (var child in node.Children)
            {
                Walk(child, learnedRecipeIds, prices, priceBasis, offersForItem,
                    recipeSheetItemIdByRecipeId, characterDisciplines, seenItemIds, opportunities);
            }
        }

        private static void TryEmit(
            CraftingTreeNode node,
            int sheetItemId,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis,
            Func<int, IReadOnlyList<VendorOffer>> offersForItem,
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines,
            HashSet<int> seenItemIds,
            List<RecipeSheetSavingsOpportunity> opportunities)
        {
            // Craft-if-crafted cost: sum of the reference branch's own
            // already-solved direct children (each child's SubtreeCost is
            // already the full recursive total for that ingredient - see
            // CraftingTreeNode.SubtreeCost). Skips display-only
            // cost-component leaves (they belong to a DIFFERENT, mixed-
            // cost vendor decision, not this hypothetical craft - see
            // IsCostComponent's own doc comment). Any child whose own cost
            // cannot be proven in coin (Currency ingredient, Unknown,
            // GuildUpgrade, unrecognized type) makes the whole craft cost
            // unprovable - bail rather than under-count it.
            //
            // Review fix (finding 6): a reference-branch child with
            // Decision == Have is now ALSO treated as unprovable (bails),
            // not "correctly contributes 0". This whole node is a
            // HYPOTHETICAL "what if I crafted instead" branch - a child
            // reported as owned may already be allocated to the REAL plan
            // elsewhere (a different node's own reduction), so this
            // hypothetical cannot safely assume those units are free AND
            // available a second time here. Treating Have as free-0 would
            // inflate SavingsPerUnit up to the full purchase price in the
            // limit (every ingredient owned), and would silently mean
            // something different under OwnMaterialsMode.Free vs. Valued
            // even though this calculator takes no ownMaterialsMode
            // parameter at all. Bailing (same as the Currency/Unknown/
            // GuildUpgrade case just above) keeps the note's own math
            // uniform regardless of that mode, and never fabricates a
            // savings number this calculator cannot actually prove.
            long craftTotal = 0;
            // Nice-to-have: counts children actually summed into
            // craftTotal (cost-component leaves don't count) - a reference
            // branch with zero of them (e.g. a mixed componentLeaves node
            // whose only real ingredient was itself a cost-component leaf)
            // would otherwise leave craftTotal/craftUnitCost at 0 and
            // report the full chosen price as SavingsPerUnit, same failure
            // shape as every other "unprovable" bail in this loop.
            int countedChildren = 0;
            foreach (var child in node.Children)
            {
                if (child.IsCostComponent)
                {
                    continue;
                }
                if (child.Decision != CraftingDecision.Craft &&
                    child.Decision != CraftingDecision.BuyFromTp &&
                    child.Decision != CraftingDecision.BuyFromVendor)
                {
                    return;
                }
                if (!child.SubtreeCost.HasValue)
                {
                    return;
                }
                // Review fix (finding 1): child.SubtreeCost already rolls
                // up an arbitrarily-deep vendor subtree (CraftingTreeBuilder
                // sets SubtreeCost = decision.TotalCost, which is the
                // COIN-only part of a BuyFromVendor decision - see
                // VendorBatchSolver.EvaluateVendorOffers' own doc comment:
                // any non-coin currency valuation "affects comparison only,
                // never the amounts committed to the plan"). A karma/spirit-
                // shard-priced descendant anywhere under this child would
                // silently vanish from craftTotal exactly like the direct-
                // child case this method already guards against just above
                // (this same rationale, applied one level up). Bail rather
                // than under-count it - mirrors the sibling check on the
                // CHOSEN side just before this loop.
                if (SubtreeHasVendorCurrencyCosts(child))
                {
                    return;
                }
                craftTotal += child.SubtreeCost.Value;
                countedChildren++;
            }
            if (countedChildren == 0)
            {
                return;
            }

            // Nice-to-have: floor (integer) division - see the matching
            // note on perSheet below for the conservative-rounding bias.
            long craftUnitCost = craftTotal / node.Quantity;
            long chosenUnitCost = node.UnitCost.Value;
            long savingsPerUnit = chosenUnitCost - craftUnitCost;
            if (savingsPerUnit <= 0)
            {
                return;
            }

            // Nice-to-have: craftUnitCost above, and perSheet here, are
            // both floor (integer) division - a conservative bias (never
            // overstates SavingsPerUnit from this rounding alone: it can
            // only understate the craft cost / overstate the sheet's own
            // cost), same undocumented-but-conservative posture as
            // VendorBatchSolver's own per-unit math. Not corrected here -
            // just flagged, since the UI presents these as exact numbers.
            // Defensive: VendorOfferStore.GetOffersForItem (the sole
            // production delegate target) never returns null (falls back
            // to Array.Empty<VendorOffer>() itself), but offersForItem is
            // now caller-suppliable (B8 narrowing) rather than a call
            // guaranteed by that one class's own contract, so a null
            // result degrades to "no offers" instead of an NRE.
            var offers = offersForItem(sheetItemId) ?? Array.Empty<VendorOffer>();
            long? cheapestSheetCost = null;
            foreach (var offer in offers)
            {
                if (offer == null || offer.OutputCount <= 0)
                {
                    continue;
                }
                // Nice-to-have: skip a seasonal-only offer for the sheet
                // itself - SeasonalOfferFilter's "the plan always assumes
                // the regular market" law (see VendorOffer.SeasonalFestival's
                // own doc comment) applies here too. No such data exists in
                // ref/vendor_offers.json today (no recipe-sheet offer is
                // seasonal), so this is a no-op guard against a future one
                // being priced as if it were available year-round.
                if (!string.IsNullOrEmpty(offer.SeasonalFestival))
                {
                    continue;
                }
                if (!CostLineValuation.TryGetCoinCost(offer.CostLines, prices, priceBasis, out long coinCost))
                {
                    continue;
                }
                long perSheet = coinCost / offer.OutputCount;
                if (!cheapestSheetCost.HasValue || perSheet < cheapestSheetCost.Value)
                {
                    cheapestSheetCost = perSheet;
                }
            }

            if (!cheapestSheetCost.HasValue)
            {
                // Not present/priceable in our vendor data - emit nothing
                // (maintainer direction: the wiki-link UI stream covers
                // discovery for this case).
                return;
            }

            bool disciplineBlocked = false;
            string discipline = null;
            int requiredRating = 0;
            var realDisciplines = node.ReferenceRecipeDisciplines?
                .Where(d => d != "MysticForge" && d != "Achievement" && d != "Merchant")
                .OrderBy(d => d, System.StringComparer.Ordinal)
                .ToList();
            if (realDisciplines != null && realDisciplines.Count > 0 && characterDisciplines != null)
            {
                requiredRating = node.ReferenceRecipeMinRating;

                // Nice-to-have: pick the candidate discipline whose best
                // account rating is CLOSEST to requiredRating, not simply
                // the alphabetically-first one - a multi-discipline recipe
                // (e.g. Armorsmith/Artificer/Huntsman/Weaponsmith) should
                // steer the player toward the discipline they are already
                // nearest to training, not whichever name sorts first.
                // Falls back to alphabetical (realDisciplines is already
                // ordinal-sorted) on an exact tie, for determinism.
                discipline = realDisciplines
                    .OrderBy(d => Math.Abs(BestAccountRating(d, characterDisciplines) - requiredRating))
                    .ThenBy(d => d, System.StringComparer.Ordinal)
                    .First();

                bool accountHasIt = characterDisciplines.Any(cd =>
                    cd != null &&
                    realDisciplines.Contains(cd.Discipline) &&
                    cd.Rating >= requiredRating);
                disciplineBlocked = !accountHasIt;
            }

            seenItemIds.Add(node.ItemId);
            opportunities.Add(new RecipeSheetSavingsOpportunity
            {
                ItemId = node.ItemId,
                RecipeId = node.ReferenceRecipeId.Value,
                SheetItemId = sheetItemId,
                SheetCost = cheapestSheetCost.Value,
                SavingsPerUnit = savingsPerUnit,
                DisciplineBlocked = disciplineBlocked,
                Discipline = discipline,
                RequiredRating = requiredRating
            });
        }

        /// <summary>
        /// True when node or any descendant beneath it carries a non-empty
        /// VendorCurrencyCosts (finding 1) - i.e. the subtree's rolled-up
        /// SubtreeCost is not fully representable in coin. Recursive
        /// because a Craft child's own SubtreeCost already sums an
        /// arbitrarily-deep chain of grandchildren, any one of which could
        /// be a BuyFromVendor node priced partly in karma/spirit shards/etc.
        /// </summary>
        private static bool SubtreeHasVendorCurrencyCosts(CraftingTreeNode node)
        {
            if (node == null)
            {
                return false;
            }
            if (node.VendorCurrencyCosts != null && node.VendorCurrencyCosts.Count > 0)
            {
                return true;
            }
            foreach (var child in node.Children)
            {
                if (SubtreeHasVendorCurrencyCosts(child))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Highest rating any character on the account has in discipline,
        /// or 0 when no character has it at all - same "0 = untrained"
        /// convention BestCharacterRating-style helpers elsewhere in this
        /// module use. characterDisciplines is guaranteed non-null by this
        /// method's sole call site.
        /// </summary>
        private static int BestAccountRating(
            string discipline, IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines)
        {
            int best = 0;
            foreach (var cd in characterDisciplines)
            {
                if (cd != null &&
                    string.Equals(cd.Discipline, discipline, StringComparison.Ordinal) &&
                    cd.Rating > best)
                {
                    best = cd.Rating;
                }
            }
            return best;
        }
    }
}
