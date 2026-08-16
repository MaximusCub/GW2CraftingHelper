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
        internal static void Apply(
            CraftingPlanResult result,
            ISet<int> learnedRecipeIds,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis,
            VendorOfferStore vendorOfferStore,
            IReadOnlyDictionary<int, int> recipeSheetItemIdByRecipeId,
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines)
        {
            var opportunities = new List<RecipeSheetSavingsOpportunity>();

            if (result == null)
            {
                return;
            }

            // Every one of these is required to safely determine "missing"
            // (learnedRecipeIds), "purchasable" (vendorOfferStore/prices),
            // or "worth the note" (recipeSheetItemIdByRecipeId non-empty) -
            // a missing input means the answer is genuinely unknown, so no
            // opportunity is ever fabricated. Mirrors BuildNotesSection's
            // own "characterDisciplines == null -> never renders a
            // competency line" null-safety contract.
            if (learnedRecipeIds != null && prices != null && vendorOfferStore != null &&
                recipeSheetItemIdByRecipeId != null && recipeSheetItemIdByRecipeId.Count > 0)
            {
                var seenItemIds = new HashSet<int>();

                if (result.CraftingTree != null)
                {
                    Walk(result.CraftingTree, learnedRecipeIds, prices, priceBasis, vendorOfferStore,
                        recipeSheetItemIdByRecipeId, characterDisciplines, seenItemIds, opportunities);
                }

                if (result.MultiItemRoots != null)
                {
                    foreach (var root in result.MultiItemRoots)
                    {
                        Walk(root, learnedRecipeIds, prices, priceBasis, vendorOfferStore,
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
            VendorOfferStore vendorOfferStore,
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
                TryEmit(node, sheetItemId, prices, priceBasis, vendorOfferStore, characterDisciplines,
                    seenItemIds, opportunities);
            }

            foreach (var child in node.Children)
            {
                Walk(child, learnedRecipeIds, prices, priceBasis, vendorOfferStore,
                    recipeSheetItemIdByRecipeId, characterDisciplines, seenItemIds, opportunities);
            }
        }

        private static void TryEmit(
            CraftingTreeNode node,
            int sheetItemId,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis,
            VendorOfferStore vendorOfferStore,
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
            // unprovable - bail rather than under-count it. An owned
            // (Have) child correctly contributes 0, not "unprovable".
            long craftTotal = 0;
            foreach (var child in node.Children)
            {
                if (child.IsCostComponent || child.Decision == CraftingDecision.Have)
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
                craftTotal += child.SubtreeCost.Value;
            }

            long craftUnitCost = craftTotal / node.Quantity;
            long chosenUnitCost = node.UnitCost.Value;
            long savingsPerUnit = chosenUnitCost - craftUnitCost;
            if (savingsPerUnit <= 0)
            {
                return;
            }

            var offers = vendorOfferStore.GetOffersForItem(sheetItemId);
            long? cheapestSheetCost = null;
            foreach (var offer in offers)
            {
                if (offer == null || offer.OutputCount <= 0)
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
                discipline = realDisciplines[0];
                requiredRating = node.ReferenceRecipeMinRating;
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
    }
}
