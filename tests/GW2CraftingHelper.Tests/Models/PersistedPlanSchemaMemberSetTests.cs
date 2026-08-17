using System;
using System.Linq;
using System.Reflection;
using GW2CraftingHelper.Models;
using Xunit;

namespace GW2CraftingHelper.Tests.Models
{
    // B1 (quality-phase1-bugs): PersistedPlan.CurrentSchemaVersion's own
    // doc comment says it must be bumped whenever a member is
    // renamed/removed/retyped on PersistedPlan, CraftingPlanResult, or
    // PlanSolveContext "in a way that would leave old data silently
    // defaulted instead of rejected" - but nothing previously enforced
    // that promise against the actual member set. PlanStoreTests only
    // ever round-trips the CURRENT model, so a future rename/remove is
    // invisible to the suite even though it is exactly the silent-default
    // failure SchemaVersion exists to reject (see the 2 -> 3 bump: the
    // persisted graph grew ~275 lines of new fields across
    // CraftingPlanResult.cs/CraftingTreeNode.cs/PlanSolveContext.cs after
    // the 1 -> 2 bump, none of which retroactively bumped the version).
    //
    // This test snapshots the public property set of every type reachable
    // from a persisted plan.json (PersistedPlan itself, plus
    // CraftingPlanResult/PlanSolveContext named explicitly in
    // CurrentSchemaVersion's doc comment, plus CraftingTreeNode - the
    // type that actually carried the CraftCostBreakdown*/BuyFromTp.../
    // BuyFromVendor... growth) as a literal list. Renaming, removing, or
    // adding a public property on any of these types changes the sorted
    // name list and fails this test - the failure message is the prompt
    // to bump CurrentSchemaVersion (and add a matching "reject the old
    // version" test alongside the existing ones in PlanStoreTests), not
    // to update the literal below to make the test pass again.
    public class PersistedPlanSchemaMemberSetTests
    {
        private static string[] PublicPropertyNames(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
        }

        [Fact]
        public void PersistedPlan_PublicMemberSet_MatchesSnapshot()
        {
            string[] expected =
            {
                "GeneratedAt",
                "IgnoredItemIds",
                "NodeOverrides",
                "PriceBasis",
                "Result",
                "RequestItems",
                "SchemaVersion",
                "UseOwnMaterials",
                "ValueOwnMaterials",
            };

            Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), PublicPropertyNames(typeof(PersistedPlan)));
        }

        [Fact]
        public void CraftingPlanResult_PublicMemberSet_MatchesSnapshot()
        {
            string[] expected =
            {
                "AcquisitionHints",
                "CharacterDisciplines",
                "CompetencyOpportunities",
                "CraftingProfit",
                "CraftingTree",
                "CurrencyMetadata",
                "DailyCooldownItems",
                "DebugLog",
                "ExcessCraftOutputs",
                "ItemMetadata",
                "MaterialOpportunityCost",
                "MultiItemRoots",
                "NetSaleValue",
                "OwnedCurrencyAmounts",
                "Plan",
                "PriceBasis",
                "ProbabilisticForgeOutputItemIds",
                "RecipeSheetSavingsOpportunities",
                "RequestedItems",
                "RequiredDisciplines",
                "RequiredRecipes",
                "SeasonalVendorTips",
                "SellableQuantity",
                "SolveContext",
                "TargetUnitSellPrice",
                "UsedMaterials",
            };

            Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), PublicPropertyNames(typeof(CraftingPlanResult)));
        }

        [Fact]
        public void PlanSolveContext_PublicMemberSet_MatchesSnapshot()
        {
            string[] expected =
            {
                "AccountItems",
                "AcquisitionHints",
                "ActiveCharacterName",
                "CharacterDisciplines",
                "CompetencyIndependentForceBuyNodeIds",
                "CurrencyMetadata",
                "CurrencyValuation",
                "DailyCooldownItems",
                "ForceBuyOnlyNodeIds",
                "HomesteadTiers",
                "LearnedRecipeIds",
                "Metadata",
                "OwnMaterialsMode",
                "OwnedCurrencyAmounts",
                "OwnedQuantityUsedByNodeId",
                "OwnedVendorItemAmounts",
                "PriceBasis",
                "Prices",
                "Quantity",
                "RequestedItems",
                "TargetItemId",
                "Tree",
                "UnreducedTree",
                "UsedMaterials",
                "VendorOffers",
            };

            Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), PublicPropertyNames(typeof(PlanSolveContext)));
        }

        [Fact]
        public void CraftingTreeNode_PublicMemberSet_MatchesSnapshot()
        {
            string[] expected =
            {
                "AcquisitionBadge",
                "AcquisitionHint",
                "BuyFromTpCostBreakdown",
                "BuyFromVendorCostBreakdown",
                "CanBuyTp",
                "CanBuyVendor",
                "CanCraft",
                "CheapestCraftDisciplines",
                "CheapestCraftMinRating",
                "CheapestCraftRealCost",
                "CheapestCraftUntrained",
                "Children",
                "ComponentOwnedQuantity",
                "CraftCostBreakdown",
                "CraftExcludedByCompetency",
                "CraftExcludedDisciplines",
                "CraftExcludedMinRating",
                "CraftExcludedRealCost",
                "CraftsNeeded",
                "Decision",
                "DecisionValue",
                "IconUrl",
                "IsAchievementBitDeduped",
                "IsCostComponent",
                "IsIgnored",
                "IsReferenceBranch",
                "ItemId",
                "Name",
                "NodeId",
                "OwnedQuantityUsed",
                "PriceSideFellBack",
                "Quantity",
                "Rarity",
                "RecipeExpectedOutputCount",
                "RecipeId",
                "RecipeOutputCount",
                "ReferenceRecipeDisciplines",
                "ReferenceRecipeId",
                "ReferenceRecipeIsLearnedFromItem",
                "ReferenceRecipeMinRating",
                "SubtreeCost",
                "UnitCost",
                "VendorComponentCostsUnreliable",
                "VendorCurrencyCosts",
            };

            Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), PublicPropertyNames(typeof(CraftingTreeNode)));
        }
    }
}
