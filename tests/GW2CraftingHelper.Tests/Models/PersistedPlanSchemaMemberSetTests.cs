using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GW2CraftingHelper.Models;
using Xunit;

namespace GW2CraftingHelper.Tests.Models
{
    // B1 (quality-phase1-bugs, quality-audit follow-up): the original guard
    // only snapshotted 4 hand-picked types (PersistedPlan/CraftingPlanResult/
    // PlanSolveContext/CraftingTreeNode) and missed every other type reachable
    // through them - RequiredRecipe, VendorOffer, PillSourceCostBreakdown, and
    // several more all grew public properties after the 1 -> 2 bump without
    // this test ever noticing. This version instead walks the full object
    // graph reachable from PersistedPlan (unwrapping List<T>/array/Nullable<T>/
    // IReadOnlyDictionary<K,V> etc.) and snapshots "Type.Property:PropertyType"
    // for every reachable Models-namespace class, so a rename, addition,
    // removal, OR retype anywhere in the persisted graph fails this test - not
    // just on the four types named in CurrentSchemaVersion's doc comment. See
    // docs/KNOWN-ISSUES.md for the full quality-audit rationale.
    public class PersistedPlanSchemaMemberSetTests
    {
        private const string ModelsNamespace = "GW2CraftingHelper.Models";

        [Fact]
        public void CurrentSchemaVersion_MatchesExpectedValue()
        {
            // Ties this snapshot to the version constant it exists to guard:
            // without this, editing the literal below to make a failing run
            // green requires no corresponding SchemaVersion bump at all.
            Assert.Equal(3, PersistedPlan.CurrentSchemaVersion);
        }

        [Fact]
        public void PersistedPlanGraph_PublicMemberSignature_MatchesSnapshot()
        {
            string[] expected =
            {
                "AcquisitionHint.Badge:String",
                "AcquisitionHint.Hint:String",
                "AcquisitionHint.ItemId:Int32",
                "AcquisitionHint.LastVerified:String",
                "AcquisitionHint.SourceUrl:String",
                "CompetencyOpportunity.CraftCost:Int64",
                "CompetencyOpportunity.DeltaCost:Int64",
                "CompetencyOpportunity.Disciplines:IReadOnlyList`1<String>",
                "CompetencyOpportunity.ItemId:Int32",
                "CompetencyOpportunity.MinRating:Int32",
                "CostLine.Count:Int32",
                "CostLine.Id:Int32",
                "CostLine.Type:String",
                "CraftingPlan.CurrencyCosts:List`1<CurrencyCost>",
                "CraftingPlan.Steps:List`1<PlanStep>",
                "CraftingPlan.TargetItemId:Int32",
                "CraftingPlan.TargetQuantity:Int32",
                "CraftingPlan.TimegatedItems:List`1<TimegatedItem>",
                "CraftingPlan.TotalCoinCost:Int64",
                "CraftingPlanResult.AcquisitionHints:IReadOnlyDictionary`2<Int32,AcquisitionHint>",
                "CraftingPlanResult.CharacterDisciplines:IReadOnlyList`1<SnapshotCharacterDiscipline>",
                "CraftingPlanResult.CompetencyOpportunities:List`1<CompetencyOpportunity>",
                "CraftingPlanResult.CraftingProfit:Nullable`1<Int64>",
                "CraftingPlanResult.CraftingTree:CraftingTreeNode",
                "CraftingPlanResult.CurrencyMetadata:IReadOnlyDictionary`2<Int32,CurrencyMetadata>",
                "CraftingPlanResult.DailyCooldownItems:IReadOnlyDictionary`2<Int32,DailyCooldownItem>",
                "CraftingPlanResult.DebugLog:List`1<String>",
                "CraftingPlanResult.ExcessCraftOutputs:List`1<ExcessCraftOutput>",
                "CraftingPlanResult.ItemMetadata:IReadOnlyDictionary`2<Int32,ItemMetadata>",
                "CraftingPlanResult.MaterialOpportunityCost:Nullable`1<Int64>",
                "CraftingPlanResult.MultiItemRoots:List`1<CraftingTreeNode>",
                "CraftingPlanResult.NetSaleValue:Nullable`1<Int64>",
                "CraftingPlanResult.OwnedCurrencyAmounts:IReadOnlyDictionary`2<Int32,Int32>",
                "CraftingPlanResult.Plan:CraftingPlan",
                "CraftingPlanResult.PriceBasis:PriceBasis",
                "CraftingPlanResult.ProbabilisticForgeOutputItemIds:List`1<Int32>",
                "CraftingPlanResult.RecipeSheetSavingsOpportunities:List`1<RecipeSheetSavingsOpportunity>",
                "CraftingPlanResult.RequestedItems:IReadOnlyList`1<PlanRequestItem>",
                "CraftingPlanResult.RequiredDisciplines:List`1<RequiredDiscipline>",
                "CraftingPlanResult.RequiredRecipes:List`1<RequiredRecipe>",
                "CraftingPlanResult.SeasonalVendorTips:List`1<SeasonalVendorTip>",
                "CraftingPlanResult.SellableQuantity:Int32",
                "CraftingPlanResult.SolveContext:PlanSolveContext",
                "CraftingPlanResult.TargetUnitSellPrice:Nullable`1<Int64>",
                "CraftingPlanResult.UsedMaterials:List`1<UsedMaterial>",
                "CraftingTreeNode.AcquisitionBadge:String",
                "CraftingTreeNode.AcquisitionHint:String",
                "CraftingTreeNode.BuyFromTpCostBreakdown:PillSourceCostBreakdown",
                "CraftingTreeNode.BuyFromVendorCostBreakdown:PillSourceCostBreakdown",
                "CraftingTreeNode.CanBuyTp:Boolean",
                "CraftingTreeNode.CanBuyVendor:Boolean",
                "CraftingTreeNode.CanCraft:Boolean",
                "CraftingTreeNode.CheapestCraftDisciplines:IReadOnlyList`1<String>",
                "CraftingTreeNode.CheapestCraftMinRating:Int32",
                "CraftingTreeNode.CheapestCraftRealCost:Nullable`1<Int64>",
                "CraftingTreeNode.CheapestCraftUntrained:Boolean",
                "CraftingTreeNode.Children:IReadOnlyList`1<CraftingTreeNode>",
                "CraftingTreeNode.ComponentOwnedQuantity:Int32",
                "CraftingTreeNode.CraftCostBreakdown:PillSourceCostBreakdown",
                "CraftingTreeNode.CraftExcludedByCompetency:Boolean",
                "CraftingTreeNode.CraftExcludedDisciplines:IReadOnlyList`1<String>",
                "CraftingTreeNode.CraftExcludedMinRating:Int32",
                "CraftingTreeNode.CraftExcludedRealCost:Nullable`1<Int64>",
                "CraftingTreeNode.CraftsNeeded:Nullable`1<Int32>",
                "CraftingTreeNode.Decision:CraftingDecision",
                "CraftingTreeNode.DecisionValue:Nullable`1<Int64>",
                "CraftingTreeNode.IconUrl:String",
                "CraftingTreeNode.IsAchievementBitDeduped:Boolean",
                "CraftingTreeNode.IsCostComponent:Boolean",
                "CraftingTreeNode.IsIgnored:Boolean",
                "CraftingTreeNode.IsReferenceBranch:Boolean",
                "CraftingTreeNode.ItemId:Int32",
                "CraftingTreeNode.Name:String",
                "CraftingTreeNode.NodeId:Int32",
                "CraftingTreeNode.OwnedQuantityUsed:Int32",
                "CraftingTreeNode.PriceSideFellBack:Boolean",
                "CraftingTreeNode.Quantity:Int32",
                "CraftingTreeNode.Rarity:String",
                "CraftingTreeNode.RecipeExpectedOutputCount:Nullable`1<Double>",
                "CraftingTreeNode.RecipeId:Nullable`1<Int32>",
                "CraftingTreeNode.RecipeOutputCount:Nullable`1<Int32>",
                "CraftingTreeNode.ReferenceRecipeDisciplines:List`1<String>",
                "CraftingTreeNode.ReferenceRecipeId:Nullable`1<Int32>",
                "CraftingTreeNode.ReferenceRecipeIsLearnedFromItem:Boolean",
                "CraftingTreeNode.ReferenceRecipeMinRating:Int32",
                "CraftingTreeNode.SubtreeCost:Nullable`1<Int64>",
                "CraftingTreeNode.UnitCost:Nullable`1<Int64>",
                "CraftingTreeNode.VendorComponentCostsUnreliable:Boolean",
                "CraftingTreeNode.VendorCurrencyCosts:IReadOnlyList`1<CostLine>",
                "CurrencyCost.Amount:Int64",
                "CurrencyCost.CurrencyId:Int32",
                "CurrencyMetadata.CurrencyId:Int32",
                "CurrencyMetadata.IconUrl:String",
                "CurrencyMetadata.Name:String",
                "CurrencyValuation.ClearedCurrencyIds:IReadOnlyCollection`1<Int32>",
                "CurrencyValuation.CopperPerUnit:IReadOnlyDictionary`2<Int32,Int64>",
                "DailyCooldownItem.ItemId:Int32",
                "DailyCooldownItem.LastVerified:String",
                "DailyCooldownItem.PerDayCap:Int32",
                "DailyCooldownItem.SourceUrl:String",
                "ExcessCraftOutput.ExcessQuantity:Int32",
                "ExcessCraftOutput.IsAccountBound:Boolean",
                "ExcessCraftOutput.ItemId:Int32",
                "ExcessCraftOutput.ReclaimValue:Nullable`1<Int64>",
                "HomesteadEfficiencyTiers.TierByMaterialId:IReadOnlyDictionary`2<Int32,Int32>",
                "ItemMetadata.IconUrl:String",
                "ItemMetadata.IsAccountBound:Boolean",
                "ItemMetadata.ItemId:Int32",
                "ItemMetadata.Name:String",
                "ItemMetadata.Rarity:String",
                "ItemPrice.BuyInstant:Int32",
                "ItemPrice.ItemId:Int32",
                "ItemPrice.SellInstant:Int32",
                "MaterialSourceAllocation.Quantity:Int32",
                "MaterialSourceAllocation.Source:String",
                "PersistedPlan.GeneratedAt:DateTime",
                "PersistedPlan.IgnoredItemIds:IReadOnlyList`1<Int32>",
                "PersistedPlan.NodeOverrides:IReadOnlyDictionary`2<Int32,AcquisitionSource>",
                "PersistedPlan.PriceBasis:PriceBasis",
                "PersistedPlan.RequestItems:IReadOnlyList`1<PlanRequestItem>",
                "PersistedPlan.Result:CraftingPlanResult",
                "PersistedPlan.SchemaVersion:Int32",
                "PersistedPlan.UseOwnMaterials:Boolean",
                "PersistedPlan.ValueOwnMaterials:Boolean",
                "PillSourceCostBreakdown.CostLines:List`1<CostLine>",
                "PillSourceCostBreakdown.DecisionValue:Nullable`1<Int64>",
                "PillSourceCostBreakdown.IsAvailable:Boolean",
                "PillSourceCostBreakdown.IsIncomplete:Boolean",
                "PillSourceCostBreakdown.RawCoin:Int64",
                "PillSourceCostBreakdown.RawQuantitiesReducedByOwnedStock:Boolean",
                "PlanRequestItem.ItemId:Int32",
                "PlanRequestItem.Quantity:Int32",
                "PlanSolveContext.AccountItems:IReadOnlyList`1<SnapshotItemEntry>",
                "PlanSolveContext.AcquisitionHints:IReadOnlyDictionary`2<Int32,AcquisitionHint>",
                "PlanSolveContext.ActiveCharacterName:String",
                "PlanSolveContext.CharacterDisciplines:IReadOnlyList`1<SnapshotCharacterDiscipline>",
                "PlanSolveContext.CompetencyIndependentForceBuyNodeIds:ISet`1<Int32>",
                "PlanSolveContext.CurrencyMetadata:IReadOnlyDictionary`2<Int32,CurrencyMetadata>",
                "PlanSolveContext.CurrencyValuation:CurrencyValuation",
                "PlanSolveContext.DailyCooldownItems:IReadOnlyDictionary`2<Int32,DailyCooldownItem>",
                "PlanSolveContext.ForceBuyOnlyNodeIds:ISet`1<Int32>",
                "PlanSolveContext.HomesteadTiers:HomesteadEfficiencyTiers",
                "PlanSolveContext.LearnedRecipeIds:ISet`1<Int32>",
                "PlanSolveContext.Metadata:IReadOnlyDictionary`2<Int32,ItemMetadata>",
                "PlanSolveContext.OwnMaterialsMode:OwnMaterialsMode",
                "PlanSolveContext.OwnedCurrencyAmounts:IReadOnlyDictionary`2<Int32,Int32>",
                "PlanSolveContext.OwnedQuantityUsedByNodeId:IReadOnlyDictionary`2<Int32,Int32>",
                "PlanSolveContext.OwnedVendorItemAmounts:IReadOnlyDictionary`2<Int32,Int32>",
                "PlanSolveContext.PriceBasis:PriceBasis",
                "PlanSolveContext.Prices:IReadOnlyDictionary`2<Int32,ItemPrice>",
                "PlanSolveContext.Quantity:Int32",
                "PlanSolveContext.RequestedItems:IReadOnlyList`1<PlanRequestItem>",
                "PlanSolveContext.TargetItemId:Int32",
                "PlanSolveContext.Tree:RecipeNode",
                "PlanSolveContext.UnreducedTree:RecipeNode",
                "PlanSolveContext.UsedMaterials:List`1<UsedMaterial>",
                "PlanSolveContext.VendorOffers:IReadOnlyDictionary`2<Int32,IReadOnlyList`1<VendorOffer>>",
                "PlanStep.ItemId:Int32",
                "PlanStep.Quantity:Int32",
                "PlanStep.RecipeId:Int32",
                "PlanStep.Source:AcquisitionSource",
                "PlanStep.TotalCost:Int64",
                "PlanStep.UnitCost:Int64",
                "PlanStep.VendorCurrencyCosts:List`1<CostLine>",
                "PlanStep.VendorOfferCurrencyCostLinesPerBatch:List`1<CostLine>",
                "PlanStep.VendorOfferOutputCount:Int32",
                "RecipeNode.AchievementBit:Nullable`1<Int32>",
                "RecipeNode.AchievementId:Nullable`1<Int32>",
                "RecipeNode.Id:Int32",
                "RecipeNode.IngredientType:String",
                "RecipeNode.IsAchievementBitDeduped:Boolean",
                "RecipeNode.IsLeaf:Boolean",
                "RecipeNode.NodeId:Int32",
                "RecipeNode.Quantity:Int32",
                "RecipeNode.Recipes:List`1<RecipeOption>",
                "RecipeOption.CraftsNeeded:Int32",
                "RecipeOption.Disciplines:List`1<String>",
                "RecipeOption.ExpectedOutputCount:Double",
                "RecipeOption.Flags:List`1<String>",
                "RecipeOption.Ingredients:List`1<RecipeNode>",
                "RecipeOption.MinRating:Int32",
                "RecipeOption.OutputCount:Int32",
                "RecipeOption.RecipeId:Int32",
                "RecipeSheetSavingsOpportunity.Discipline:String",
                "RecipeSheetSavingsOpportunity.DisciplineBlocked:Boolean",
                "RecipeSheetSavingsOpportunity.ItemId:Int32",
                "RecipeSheetSavingsOpportunity.RecipeId:Int32",
                "RecipeSheetSavingsOpportunity.RequiredRating:Int32",
                "RecipeSheetSavingsOpportunity.SavingsPerUnit:Int64",
                "RecipeSheetSavingsOpportunity.SheetCost:Int64",
                "RecipeSheetSavingsOpportunity.SheetItemId:Int32",
                "RequiredDiscipline.Discipline:String",
                "RequiredDiscipline.MinRating:Int32",
                "RequiredRecipe.Disciplines:List`1<String>",
                "RequiredRecipe.IsAutoLearned:Boolean",
                "RequiredRecipe.IsLearnedFromItem:Boolean",
                "RequiredRecipe.IsMissing:Nullable`1<Boolean>",
                "RequiredRecipe.MinRating:Int32",
                "RequiredRecipe.OutputItemId:Int32",
                "RequiredRecipe.RecipeId:Int32",
                "SeasonalVendorTip.CostLines:List`1<CostLine>",
                "SeasonalVendorTip.DailyCap:Nullable`1<Int32>",
                "SeasonalVendorTip.Festival:String",
                "SeasonalVendorTip.ItemId:Int32",
                "SeasonalVendorTip.MerchantName:String",
                "SeasonalVendorTip.OfferUnitCost:Int64",
                "SeasonalVendorTip.OutputCount:Int32",
                "SeasonalVendorTip.PlanUnitPrice:Int64",
                "SeasonalVendorTip.WeeklyCap:Nullable`1<Int32>",
                "SnapshotCharacterDiscipline.Active:Boolean",
                "SnapshotCharacterDiscipline.CharacterName:String",
                "SnapshotCharacterDiscipline.Discipline:String",
                "SnapshotCharacterDiscipline.Rating:Int32",
                "SnapshotItemEntry.Count:Int32",
                "SnapshotItemEntry.IconUrl:String",
                "SnapshotItemEntry.ItemId:Int32",
                "SnapshotItemEntry.Name:String",
                "SnapshotItemEntry.Source:String",
                "TimegatedItem.CapType:TimegatedCapType",
                "TimegatedItem.CapValue:Int32",
                "TimegatedItem.ItemId:Int32",
                "TimegatedItem.NeededCount:Int32",
                "UsedMaterial.ItemId:Int32",
                "UsedMaterial.QuantityUsed:Int32",
                "UsedMaterial.Sources:List`1<MaterialSourceAllocation>",
                "VendorOffer.CostLines:List`1<CostLine>",
                "VendorOffer.DailyCap:Nullable`1<Int32>",
                "VendorOffer.HomesteadTier:Nullable`1<Int32>",
                "VendorOffer.Locations:List`1<String>",
                "VendorOffer.MerchantName:String",
                "VendorOffer.OfferId:String",
                "VendorOffer.OutputCount:Int32",
                "VendorOffer.OutputItemId:Int32",
                "VendorOffer.SeasonalCap:Nullable`1<Int32>",
                "VendorOffer.SeasonalFestival:String",
                "VendorOffer.WeeklyCap:Nullable`1<Int32>",
            };

            string[] actual = ReachableModelTypes(typeof(PersistedPlan))
                .SelectMany(MemberSignatures)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected.OrderBy(s => s, StringComparer.Ordinal), actual);
        }

        private static IReadOnlyCollection<Type> ReachableModelTypes(Type root)
        {
            var visited = new HashSet<Type>();
            var queue = new Queue<Type>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Type type = queue.Dequeue();
                if (!visited.Add(type))
                {
                    continue;
                }

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    foreach (Type candidate in UnwrapModelTypes(property.PropertyType))
                    {
                        queue.Enqueue(candidate);
                    }
                }
            }

            return visited;
        }

        private static IEnumerable<Type> UnwrapModelTypes(Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                type = underlying;
            }

            if (type.IsArray)
            {
                foreach (Type inner in UnwrapModelTypes(type.GetElementType()))
                {
                    yield return inner;
                }

                yield break;
            }

            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    foreach (Type inner in UnwrapModelTypes(argument))
                    {
                        yield return inner;
                    }
                }

                yield break;
            }

            if (type.IsClass && type.Namespace == ModelsNamespace)
            {
                yield return type;
            }
        }

        private static IEnumerable<string> MemberSignatures(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => type.Name + "." + p.Name + ":" + Describe(p.PropertyType));
        }

        // Retype-blind-spot fix (quality-phase1-bugs): Type.Name alone drops
        // generic arguments (List<CurrencyCost> and List<ItemMetadata> both
        // report "List`1"), so retyping an element/key/value type anywhere
        // reachable in the graph was silently invisible whenever both the
        // old and new element types were themselves reachable elsewhere in
        // the same snapshot. Describe recurses into generic arguments so
        // the signature captures the full shape (e.g. "List`1<CurrencyCost>",
        // "Nullable`1<Int64>").
        private static string Describe(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }

            string args = string.Join(",", type.GetGenericArguments().Select(Describe));
            return type.Name + "<" + args + ">";
        }
    }
}
