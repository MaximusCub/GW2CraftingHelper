using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The one-way floor that holds a row's IGNORE key still while a plan
    /// is on screen. What it has to survive is the click that ignores a
    /// row: the solver returns that node owned, so its source pills are
    /// gone and the run the key is seated against collapses.
    /// </summary>
    public class TreePillRunInkFloorTests
    {
        // The stand-in face TreePillRunLayoutTests drives these same
        // functions with: one pixel per character, plus the renderer's own
        // pill padding and inter-pill gap.
        private const int Padding = 12;
        private const int Gap = 6;

        /// <summary>Width of the run the IGNORE key is seated against:
        /// every pill the planner emits except the key itself.</summary>
        private static int RunInk(CraftingTreeNode node)
        {
            var specs = DecisionPillPlanner.BuildPillSpecs(node);
            int ink = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].Kind != PillKind.Ignore)
                {
                    ink += specs[i].Text.Length + Padding + Gap;
                }
            }

            return ink > 0 ? ink - Gap : 0;
        }

        private static CraftingTreeNode LiveNode()
        {
            return new CraftingTreeNode
            {
                NodeId = 7,
                ItemId = 19721,
                Quantity = 5,
                Decision = CraftingDecision.BuyFromTp,
                CanCraft = true,
                CanBuyTp = true,
            };
        }

        private static CraftingTreeNode IgnoredNode()
        {
            return new CraftingTreeNode
            {
                NodeId = 7,
                ItemId = 19721,
                Quantity = 5,
                Decision = CraftingDecision.Have,
                IsIgnored = true,
            };
        }

        [Fact]
        public void TheRunThatCollapsesOnAnIgnoreClick_DoesNotNarrowTheFloor()
        {
            var floor = new TreePillRunInkFloor();
            int live = RunInk(LiveNode());
            int ignored = RunInk(IgnoredNode());
            Assert.True(live > ignored);

            Assert.Equal(live, floor.Widen(7, live));
            Assert.Equal(live, floor.Widen(7, ignored));
            Assert.Equal(live, floor.Widen(7, live));
        }

        [Fact]
        public void AWiderRunRaisesTheFloor()
        {
            var floor = new TreePillRunInkFloor();

            Assert.Equal(40, floor.Widen(7, 40));
            Assert.Equal(90, floor.Widen(7, 90));
            Assert.Equal(90, floor.Widen(7, 10));
        }

        [Fact]
        public void EachRowKeepsItsOwnFloor()
        {
            var floor = new TreePillRunInkFloor();

            Assert.Equal(200, floor.Widen(1, 200));
            Assert.Equal(30, floor.Widen(2, 30));
            Assert.Equal(200, floor.Widen(1, 30));
        }

        /// <summary>A fresh Generate starts again from nothing, so a new
        /// plan's rows are seated on their own runs.</summary>
        [Fact]
        public void ClearForgetsEveryRow()
        {
            var floor = new TreePillRunInkFloor();
            floor.Widen(7, 200);

            floor.Clear();

            Assert.Equal(30, floor.Widen(7, 30));
        }
    }
}
