using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RecipeNodeBuilders;

namespace TaimisToolbench.Tests.Services
{
    public class OwnedMaterialsForceBuyPrePassTests
    {
        // Leaf/Craftable/Option come from Helpers/RecipeNodeBuilders.cs.
        [Fact]
        public void BuyBeatsCraftByMoreThan15Percent_ForcesRootIntoForceBuySet()
        {
            // Root buy = 100; components (2x30=60) cost less than 85 (0.85 x 100).
            // 100 < 60*... wait - the rule is buy < craft*0.85: buy=60, craft=100
            // would need buy cheaper - construct so BUY is the cheap side:
            // buy=100, craft(components fresh)=200 -> 100 < 200*0.85=170 -> forced.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
            };
            var solver = new PlanSolver();

            var result = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.Contains(0, result.ForceBuyOnlyNodeIds); // root NodeId
            // No discipline
            // requirement anywhere in this tree, so competency can never
            // demote anything - the competency-resolved and competency-
            // blind evaluations are identical, and this node is forced
            // under BOTH.
            Assert.Contains(0, result.CompetencyIndependentForceBuyNodeIds);
        }

        [Fact]
        public void BuyBeatsCraftByLessThan15Percent_NotForced()
        {
            // buy=95, craft=100 -> 95 < 100*0.85=85? No (95 > 85) - not forced,
            // even though buy is still cheaper than craft outright.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 95 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
            };
            var solver = new PlanSolver();

            var result = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.DoesNotContain(0, result.ForceBuyOnlyNodeIds);
            Assert.DoesNotContain(0, result.CompetencyIndependentForceBuyNodeIds);
        }

        [Fact]
        public void CraftCheaperThanBuy_NotForced()
        {
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } },
            };
            var solver = new PlanSolver();

            var result = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.Empty(result.ForceBuyOnlyNodeIds);
            Assert.Empty(result.CompetencyIndependentForceBuyNodeIds);
        }

        // (Critical #3, source-selection-
        // simplification): before this fix, ComputeForceBuyOnlyNodeIds had
        // no characterDisciplines parameter at all, so this throwaway
        // solve was the ONLY solve of a generation that stayed
        // competency-UNKNOWN - a not-actually-craftable CHILD ingredient's
        // own decision inside this solve could commit Craft (its cheap,
        // untrained price) purely because competency was never checked
        // here, folding that cheap price into the PARENT's own craftCost
        // and silently skewing the 85% force-buy comparison relative to
        // what the real (competency-aware) solve would ever produce.
        [Fact]
        public void ChildIngredientNotCraftable_CharacterDisciplinesThreaded_ChangesForceBuyResult()
        {
            // Parent (item 1) craft-only ingredient is item 2, itself
            // craftable via a Weaponsmith-500 recipe (cheap, 5c) or
            // buyable at 200c. Nobody on the account is trained.
            var childCraftable = Craftable(2, 1,
                Option(20, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(3, 1)));
            var tree = Craftable(1, 1, Option(10, 1, 1, childCraftable));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 150 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 200 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 5 } },
            };
            var solver = new PlanSolver();
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 100 },
            };

            // Without characterDisciplines: the pre-pass's own solve is
            // competency-UNKNOWN, so item 2 commits Craft@5 (cheapest,
            // never checked) - parent's craftCost reads 5, nowhere near
            // buy's 150, so root is NOT forced (150 is not < 5*0.85).
            var resultWithout = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);
            Assert.DoesNotContain(0, resultWithout.ForceBuyOnlyNodeIds);

            // With characterDisciplines threaded through: item 2's own
            // Craft option is now correctly competency-excluded (untrained,
            // a genuine buy alternative exists), so it commits
            // BuyFromTp@200 instead - parent's craftCost reads 200, and
            // 150 < 200*0.85=170, so root NOW gets force-buy-flagged. The
            // two calls produce DIFFERENT results purely because of this
            // parameter, proving it actually reaches the child's own
            // decision inside this throwaway solve.
            //
            // Root (item 1) has only ONE recipe option of its own (no
            // discipline requirement), so its OWN competency-resolved and
            // competency-blind evaluations always agree (both read the
            // SAME single-recipe craftCost) regardless of what the CHILD
            // committed to further down - root therefore also lands in
            // CompetencyIndependentForceBuyNodeIds here.
            var resultWith = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null,
                characterDisciplines: characterDisciplines);
            Assert.Contains(0, resultWith.ForceBuyOnlyNodeIds);
            Assert.Contains(0, resultWith.CompetencyIndependentForceBuyNodeIds);
        }

        [Fact]
        public void NoRecipe_NeverForced()
        {
            // A leaf with no recipe has no craftCost at all - never forced,
            // regardless of its buy price.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10 } },
            };
            var solver = new PlanSolver();

            var result = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.Empty(result.ForceBuyOnlyNodeIds);
            Assert.Empty(result.CompetencyIndependentForceBuyNodeIds);
        }

        [Fact]
        public void NoBuyPrice_NeverForced()
        {
            // No TP buy price at all for the root - can't compare against
            // craft cost, so it can never be forced (matches "buy < craft
            // requires buy to actually exist").
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
            };
            var solver = new PlanSolver();

            var result = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.Empty(result.ForceBuyOnlyNodeIds);
            Assert.Empty(result.CompetencyIndependentForceBuyNodeIds);
        }
    }
}
