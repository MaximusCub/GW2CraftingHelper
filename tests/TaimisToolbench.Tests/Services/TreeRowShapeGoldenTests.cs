using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Every tree row's computed shape - column origins, caret glyph,
    /// quantity prefix, expansion default and the dimmed-branch flag -
    /// swept across depth, dimming, child count, quantity, decision and
    /// expansion override.
    ///
    /// These decisions used to be inline arithmetic inside
    /// TreeSectionController.RenderTreeNode, where no test could reach them
    /// without a Blish control tree. The golden below was written from the
    /// pre-extraction renderer source, so an identical sweep is evidence
    /// that moving the arithmetic into TreeRowShapePlanner moved no pixel -
    /// not merely that the renderer still compiles.
    ///
    /// Re-anchored twice since, each time by editing one column and
    /// nothing else: nameX +8 for the tier-2 icon resize
    /// (ItemIconTiers.BagSidebarIconSize grew IconFrameSize 34 -> 42), and
    /// the ruleX column deleted when the dimmed subtree's left-indent rule
    /// was removed. Every surviving column is byte-identical to the
    /// original sweep.
    /// </summary>
    public class TreeRowShapeGoldenTests
    {
        [Fact]
        public void TheSweepIsUnchanged()
        {
            string goldenPath = Path.Combine(AppContext.BaseDirectory, "Goldens", "tree-row-shape-sweep.txt");
            Assert.True(File.Exists(goldenPath), "Golden not found at " + goldenPath);

            var expected = File.ReadAllLines(goldenPath);
            var actual = BuildSweep();

            Assert.Equal(expected.Length, actual.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                // Line by line so a failure names the case that moved
                // rather than dumping the whole sweep.
                Assert.Equal(expected[i], actual[i]);
            }
        }

        /// <summary>
        /// The column header sits above a depth-0 row's name, so the offset
        /// it uses and the offset a row uses must be the same number.
        /// </summary>
        [Fact]
        public void NameColumnOffsetMatchesADepthZeroRowsNameX()
        {
            var shape = TreeRowShapePlanner.Plan(Node(CraftingDecision.Craft), depth: 0, dimmed: false, expansionOverrides: null);
            Assert.Equal(TreeRowShapePlanner.NameColumnOffset, shape.NameX);
        }

        internal static List<string> BuildSweep()
        {
            var lines = new List<string>
            {
                "depth|dimmed|children|qty|decision|override|indent|iconX|nameX|caret|expanded|qtyPrefix|childDimmed",
            };

            var decisions = new[]
            {
                CraftingDecision.Craft,
                CraftingDecision.BuyFromTp,
                CraftingDecision.Have,
                CraftingDecision.Currency,
            };

            for (int depth = 0; depth <= 4; depth++)
            {
                foreach (bool dimmed in new[] { false, true })
                {
                    foreach (int childCount in new[] { 0, 2 })
                    {
                        foreach (int quantity in new[] { 0, 1, 12 })
                        {
                            foreach (var decision in decisions)
                            {
                                foreach (var over in new bool?[] { null, true, false })
                                {
                                    var node = Node(decision, quantity, childCount);
                                    IReadOnlyDictionary<int, bool> overrides = over.HasValue
                                        ? new Dictionary<int, bool> { { node.NodeId, over.Value } }
                                        : null;

                                    var shape = TreeRowShapePlanner.Plan(node, depth, dimmed, overrides);

                                    var sb = new StringBuilder(96);
                                    sb.Append(depth).Append('|')
                                        .Append(dimmed).Append('|')
                                        .Append(childCount).Append('|')
                                        .Append(quantity).Append('|')
                                        .Append(decision).Append('|')
                                        .Append(over.HasValue ? over.Value.ToString() : "none").Append('|')
                                        .Append(shape.Indent).Append('|')
                                        .Append(shape.IconX).Append('|')
                                        .Append(shape.NameX).Append('|')
                                        .Append(shape.CaretGlyph ?? "none").Append('|')
                                        .Append(shape.IsExpanded).Append('|')
                                        .Append(shape.QuantityPrefix.Length == 0 ? "none" : shape.QuantityPrefix).Append('|')
                                        .Append(shape.ChildDimmed);
                                    lines.Add(sb.ToString());
                                }
                            }
                        }
                    }
                }
            }

            return lines;
        }

        private static CraftingTreeNode Node(CraftingDecision decision, int quantity = 1, int childCount = 0)
        {
            var children = new List<CraftingTreeNode>();
            for (int i = 0; i < childCount; i++)
            {
                children.Add(new CraftingTreeNode { NodeId = 100 + i, ItemId = 100 + i, Quantity = 1 });
            }

            return new CraftingTreeNode
            {
                NodeId = 7,
                ItemId = 7,
                Name = "Test Item",
                Quantity = quantity,
                Decision = decision,
                Children = children,
            };
        }
    }
}
