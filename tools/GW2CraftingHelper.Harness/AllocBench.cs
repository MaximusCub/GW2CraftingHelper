using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Harness
{
    /// <summary>
    /// Allocation microbenchmarks for the Blish-free per-interaction paths
    /// (layout math, tooltip composition, row shaping, pill planning,
    /// sorting, coin formatting), driven with a REAL plan produced by the
    /// real pipeline so input sizes are realistic rather than synthetic.
    ///
    /// Counter: AppDomain.MonitoringTotalAllocatedMemorySize (net48's
    /// cumulative allocated-bytes counter; GC.GetAllocatedBytesForCurrentThread
    /// does not exist on net48). The counter is only guaranteed accurate
    /// after a collection, so every reading is fenced with a full
    /// GC.Collect. Benchmarks run single-threaded with large N so any
    /// residual imprecision amortises to noise.
    /// </summary>
    internal static class AllocBench
    {
        public static async Task RunAsync(
            CraftingPlanPipeline pipeline, ProfileItem item, PlanSolver solver)
        {
            AppDomain.MonitoringIsEnabled = true;

            Console.WriteLine($"=== Allocation benchmarks (input: {item.Name} x{item.Quantity}, offline) ===");
            Console.WriteLine();

            var result = await pipeline.GenerateStructuredAsync(
                item.ItemId, item.Quantity, null, CancellationToken.None);
            var vmBuilder = new PlanViewModelBuilder();
            var vm = vmBuilder.Build(result);

            var nodes = new List<NodeAtDepth>();
            Flatten(vm.TreeRoot, 0, nodes);
            if (vm.MultiItemRoots != null)
            {
                nodes.Clear();
                foreach (var root in vm.MultiItemRoots)
                {
                    Flatten(root, 0, nodes);
                }
            }

            var roots = vm.MultiItemRoots ?? new List<CraftingTreeNode> { vm.TreeRoot };
            var noOverrides = new Dictionary<int, bool>();

            IReadOnlyList<PlanRowViewModel> shoppingRows = null;
            foreach (var section in vm.Sections)
            {
                if (section.SectionType == PlanSectionType.ShoppingList)
                {
                    shoppingRows = section.Rows;
                }
            }

            shoppingRows = shoppingRows ?? new List<PlanRowViewModel>();

            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "Input scale: {0} tree nodes, {1} shopping rows, {2} sections",
                nodes.Count, shoppingRows.Count, vm.Sections.Count));
            Console.WriteLine();
            Console.WriteLine("| Path | N ops | bytes/op | us/op |");
            Console.WriteLine("|---|---|---|---|");

            // 1. Full pipeline generate (offline, warm caches).
            Measure("GenerateStructuredAsync (full offline pipeline)", 5, () =>
            {
                pipeline.GenerateStructuredAsync(
                    item.ItemId, item.Quantity, null, CancellationToken.None)
                    .GetAwaiter().GetResult();
            });

            // 2. Solver alone, on the real pre-solve tree.
            var solveTree = result.SolveContext?.Tree;
            var solvePrices = result.SolveContext?.Prices
                ?? new Dictionary<int, ItemPrice>();
            if (solveTree != null)
            {
                Measure("PlanSolver.Solve (real tree + prices)", 20, () =>
                {
                    solver.Solve(solveTree, solvePrices);
                });
            }

            // 3. View model build from the finished result.
            Measure("PlanViewModelBuilder.Build", 50, () =>
            {
                vmBuilder.Build(result);
            });

            // 4. Tree row shaping, every node (one full relayout's worth).
            Measure(
                $"TreeRowShapePlanner.Plan x{nodes.Count} (full tree)", 200, () =>
            {
                foreach (var n in nodes)
                {
                    TreeRowShapePlanner.Plan(n.Node, n.Depth, false, noOverrides);
                }
            });

            // 5. Tree height math (what a toggle/relayout recomputes).
            Measure("PlanContentHeightMath.MultiRootTreeFlowHeight", 200, () =>
            {
                PlanContentHeightMath.MultiRootTreeFlowHeight(roots, noOverrides);
            });

            // 6. Pill planning, every node (one full render's worth).
            Measure(
                $"DecisionPillPlanner.BuildPillSpecs x{nodes.Count} (full tree)", 100, () =>
            {
                foreach (var n in nodes)
                {
                    DecisionPillPlanner.BuildPillSpecs(
                        n.Node, vm.CurrencyPlanTotals, vm.OwnedCurrencyAmounts);
                }
            });

            // 7. Table sort of the shopping list (one header click).
            var sortState = new TableSortState<PlanTableColumn>();
            sortState.Cycle(PlanTableColumn.Total);
            Measure(
                $"PlanTableSorter.Sort ({shoppingRows.Count} shopping rows)", 500, () =>
            {
                PlanTableSorter.Sort(shoppingRows, sortState);
            });

            // 8. Coin formatting across the shopping rows' real amounts.
            var coinAmounts = new List<long>();
            foreach (var row in shoppingRows)
            {
                coinAmounts.Add(row.CoinValue);
                coinAmounts.Add(row.UnitCoinValue);
            }

            if (coinAmounts.Count == 0)
            {
                coinAmounts.AddRange(new long[] { 0, 99, 1005, 10000, 1234567 });
            }

            Measure(
                $"CoinSegmentMath.GameStyleText x{coinAmounts.Count}", 2000, () =>
            {
                foreach (long c in coinAmounts)
                {
                    CoinSegmentMath.GameStyleText(c);
                }
            });

            Measure(
                $"CoinSegmentMath.FormatSegmentTexts x{coinAmounts.Count}", 2000, () =>
            {
                foreach (long c in coinAmounts)
                {
                    CoinSegmentMath.FormatSegmentTexts(c);
                }
            });

            // 9. Tooltip composition, every node (hover cost paid per row;
            // a full sweep approximates one pass over the whole tree).
            Measure(
                $"TreeRowTooltipComposer.BuildExtraTooltipContent x{nodes.Count}", 50, () =>
            {
                foreach (var n in nodes)
                {
                    TreeRowTooltipComposer.BuildExtraTooltipContent(
                        n.Node, "Crafted in-game via its recipe.", vm);
                }
            });

            // 10. Tooltip wrapping over the real composed strings.
            var tooltipTexts = new List<string>();
            foreach (var n in nodes)
            {
                var content = TreeRowTooltipComposer.BuildExtraTooltipContent(
                    n.Node, "Crafted in-game via its recipe.", vm);
                string text = JoinLines(content);
                if (!string.IsNullOrEmpty(text))
                {
                    tooltipTexts.Add(text);
                }
            }

            Measure(
                $"TooltipTextFormat.Wrap x{tooltipTexts.Count} (real tooltip texts)", 100, () =>
            {
                foreach (var t in tooltipTexts)
                {
                    TooltipTextFormat.Wrap(t);
                }
            });

            Console.WriteLine();
        }

        private struct NodeAtDepth
        {
            public CraftingTreeNode Node;
            public int Depth;
        }

        private static void Flatten(CraftingTreeNode node, int depth, List<NodeAtDepth> into)
        {
            if (node == null)
            {
                return;
            }

            into.Add(new NodeAtDepth { Node = node, Depth = depth });
            foreach (var child in node.Children)
            {
                Flatten(child, depth + 1, into);
            }
        }

        private static string JoinLines(TooltipContent content)
        {
            if (content == null || content.IsEmpty)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (var line in content.Lines)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                foreach (var span in line.Spans)
                {
                    sb.Append(span.Text);
                }
            }

            return sb.ToString();
        }

        private static void Measure(string name, int iterations, Action op)
        {
            // Warm up (JIT + lazy statics) outside the measured window.
            op();
            op();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long bytesBefore = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                op();
            }

            sw.Stop();

            // The monitoring counter is only guaranteed current after a
            // collection; fence before the closing read.
            GC.Collect();
            long bytesAfter = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;

            long perOp = (bytesAfter - bytesBefore) / iterations;
            double usPerOp = sw.Elapsed.TotalMilliseconds * 1000.0 / iterations;

            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "| {0} | {1} | {2:N0} | {3:N1} |",
                name, iterations, perOp, usPerOp));
        }
    }
}
