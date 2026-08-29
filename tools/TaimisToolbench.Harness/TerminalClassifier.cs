using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;

namespace TaimisToolbench.Harness
{
    /// <summary>
    /// Buckets a solved plan's terminals by the route the solver committed
    /// to, so a tree can be scored for how much of it the module can
    /// actually price. A terminal is any node the solver did NOT decide to
    /// craft: crafting is the only decision that expands into real
    /// children, so a non-Craft node is where the plan stops and the player
    /// has to go get something.
    /// </summary>
    internal enum TerminalBucket
    {
        /// <summary>Priced from the Trading Post.</summary>
        TradingPost,

        /// <summary>Priced from a vendor, coin only.</summary>
        VendorCoin,

        /// <summary>Priced from a vendor whose non-coin lines all carry a valuation.</summary>
        VendorValuedCurrency,

        /// <summary>Vendor route present, but at least one cost line has no valuation.</summary>
        VendorUnvalued,

        /// <summary>UnknownSource: the solver found no route at all.</summary>
        Unknown,

        /// <summary>Owned, ignored, or deduplicated - no acquisition decision was made.</summary>
        Have,

        /// <summary>A raw currency ingredient (karma, coin) rather than an item.</summary>
        CurrencyLeaf,

        /// <summary>GuildUpgrade or UnrecognizedIngredient - representable, not priceable.</summary>
        OtherUnpriced,
    }

    internal class TerminalRecord
    {
        public int ItemId { get; set; }

        public string Name { get; set; }

        public int Quantity { get; set; }

        public TerminalBucket Bucket { get; set; }

        public string Badge { get; set; }

        /// <summary>
        /// For VendorUnvalued: the cost lines that had no valuation, as
        /// "Currency:id" / "Item:id" tokens. Empty for every other bucket.
        /// </summary>
        public List<string> UnvaluedLines { get; } = new List<string>();

        /// <summary>
        /// True for a vendor terminal whose coin cost is zero or absent -
        /// the whole price is non-coin, so the node contributes nothing to
        /// the plan's gold total no matter how large the real price is.
        /// </summary>
        public bool ZeroCoin { get; set; }

        /// <summary>
        /// The non-coin lines this vendor terminal is actually charged.
        /// Each Count is already scaled to the node's demand.
        /// </summary>
        public List<CostLine> CostLines { get; } = new List<CostLine>();
    }

    internal class ItemClassification
    {
        public string Name { get; set; }

        public int ItemId { get; set; }

        public bool Planned { get; set; }

        public string Error { get; set; }

        public long SolveMs { get; set; }

        public int PlanNodeCount { get; set; }

        public int PlanDepth { get; set; }

        public int RootRecipeCount { get; set; }

        public bool RootNameResolved { get; set; }

        public int ProbabilisticForgeOutputs { get; set; }

        public CraftingDecision RootDecision { get; set; }

        public bool RootCanCraft { get; set; }

        /// <summary>
        /// True when this row came from a re-solve that pinned the root to
        /// Craft (see --force-craft-root). The natural run buys a legendary
        /// outright whenever the Trading Post beats the craft tree, and a
        /// bought root has no expanded tree to classify at all.
        /// </summary>
        public bool RootForcedToCraft { get; set; }

        /// <summary>The plan's whole displayed gold total, in copper.</summary>
        public long? RootSubtreeCost { get; set; }

        public List<TerminalRecord> Terminals { get; } = new List<TerminalRecord>();

        public int Count(TerminalBucket bucket)
        {
            return Terminals.Count(t => t.Bucket == bucket);
        }
    }

    internal static class TerminalClassifier
    {
        /// <summary>
        /// The terminals this experiment is about: a route the module
        /// cannot put a comparable price on. Defined once because both the
        /// per-item detail and the cross-item ranking select on it, and a
        /// drift between the two would silently mis-rank the findings.
        /// </summary>
        private static bool IsBlocker(TerminalRecord t)
        {
            return t.Bucket == TerminalBucket.Unknown ||
                t.Bucket == TerminalBucket.VendorUnvalued ||
                t.Bucket == TerminalBucket.OtherUnpriced;
        }

        /// <summary>
        /// Solves every item once and prints per-item bucket counts, a
        /// per-item breakdown of the unpriceable terminals, and a
        /// cross-item aggregate ranking each blocking item by how many
        /// trees it appears in.
        /// </summary>
        public static async Task RunAsync(
            CraftingPlanPipeline pipeline,
            List<ProfileItem> items,
            string mode,
            CurrencyValuation valuation,
            HomesteadEfficiencyTiers homesteadTiers,
            bool forceCraftRoot)
        {
            var classifications = new List<ItemClassification>();
            foreach (var item in items)
            {
                classifications.Add(
                    await ClassifyAsync(pipeline, item, valuation, homesteadTiers, forceCraftRoot));
            }

            PrintPerItemTable(classifications, mode);
            Console.WriteLine();
            PrintPerItemDetail(classifications);
            Console.WriteLine();
            PrintAggregate(classifications);
        }

        private static async Task<ItemClassification> ClassifyAsync(
            CraftingPlanPipeline pipeline,
            ProfileItem item,
            CurrencyValuation valuation,
            HomesteadEfficiencyTiers homesteadTiers,
            bool forceCraftRoot)
        {
            var c = new ItemClassification { Name = item.Name, ItemId = item.ItemId };
            var sw = Stopwatch.StartNew();
            CraftingPlanResult result;
            try
            {
                result = await pipeline.GenerateStructuredAsync(
                    item.ItemId, item.Quantity, null, CancellationToken.None,
                    currencyValuation: valuation, homesteadTiers: homesteadTiers);
            }
            catch (Exception ex)
            {
                sw.Stop();
                c.SolveMs = sw.ElapsedMilliseconds;
                c.Error = ex.GetType().Name + ": " + ex.Message;
                return c;
            }

            sw.Stop();
            c.SolveMs = sw.ElapsedMilliseconds;

            if (result == null || result.CraftingTree == null)
            {
                c.Error = "pipeline returned no crafting tree";
                return c;
            }

            if (forceCraftRoot &&
                result.CraftingTree.Decision != CraftingDecision.Craft &&
                result.CraftingTree.CanCraft &&
                result.SolveContext != null)
            {
                var overrides = new Dictionary<int, AcquisitionSource>
                {
                    { result.SolveContext.Tree.NodeId, AcquisitionSource.Craft },
                };
                var forced = pipeline.ResolveWithOverrides(result.SolveContext, overrides);
                if (forced != null && forced.CraftingTree != null)
                {
                    result = forced;
                    c.RootForcedToCraft = true;
                }
            }

            c.Planned = true;
            c.RootDecision = result.CraftingTree.Decision;
            c.RootCanCraft = result.CraftingTree.CanCraft;
            c.ProbabilisticForgeOutputs = result.ProbabilisticForgeOutputItemIds?.Count ?? 0;
            c.RootSubtreeCost = result.CraftingTree.SubtreeCost;

            var rawRoot = result.SolveContext?.Tree;
            c.RootRecipeCount = rawRoot != null ? rawRoot.Recipes.Count : -1;

            string rootName = result.CraftingTree.Name;
            c.RootNameResolved = !string.IsNullOrEmpty(rootName) &&
                !rootName.StartsWith("Unknown Item", StringComparison.Ordinal);

            Walk(result.CraftingTree, 1, c, valuation);
            return c;
        }

        /// <summary>
        /// Mirrors Program.MaxDumpDepth. A solved tree is acyclic by
        /// construction, so this only ever fires if that stops being true -
        /// and an unbounded walk would take the process down with a stack
        /// overflow, which no catch block can recover.
        /// </summary>
        private const int MaxWalkDepth = 100;

        private static void Walk(
            CraftingTreeNode node, int depth, ItemClassification c, CurrencyValuation valuation)
        {
            // A cost-component leaf is display-only synthesis under a vendor
            // node (CraftingTreeNode.IsCostComponent) - it describes what
            // the offer charges, not a separate thing to acquire, so
            // counting it would double-count the vendor terminal above it.
            if (node.IsCostComponent || depth > MaxWalkDepth)
            {
                return;
            }

            c.PlanNodeCount++;
            if (depth > c.PlanDepth)
            {
                c.PlanDepth = depth;
            }

            if (node.Decision == CraftingDecision.Craft && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    Walk(child, depth + 1, c, valuation);
                }

                return;
            }

            // Every non-Craft decision stops the plan here. Children below a
            // bought node are the dimmed reference branch (what crafting it
            // WOULD have cost) and are not part of the shopping the player
            // has to do, so the walk does not descend into them.
            c.Terminals.Add(BuildRecord(node, valuation));
        }

        private static TerminalRecord BuildRecord(CraftingTreeNode node, CurrencyValuation valuation)
        {
            var record = new TerminalRecord
            {
                ItemId = node.ItemId,
                Name = string.IsNullOrEmpty(node.Name) ? "(unnamed)" : node.Name,
                Quantity = node.Quantity,
                Badge = node.AcquisitionBadge,
            };

            if (node.Decision == CraftingDecision.BuyFromVendor)
            {
                record.ZeroCoin = !node.SubtreeCost.HasValue || node.SubtreeCost.Value == 0L;
                if (node.VendorCurrencyCosts != null)
                {
                    record.CostLines.AddRange(node.VendorCurrencyCosts);
                }
            }

            switch (node.Decision)
            {
                case CraftingDecision.BuyFromTp:
                    record.Bucket = TerminalBucket.TradingPost;
                    break;
                case CraftingDecision.BuyFromVendor:
                    ClassifyVendor(node, valuation, record);
                    break;
                case CraftingDecision.Unknown:
                    record.Bucket = TerminalBucket.Unknown;
                    break;
                case CraftingDecision.Have:
                    record.Bucket = TerminalBucket.Have;
                    break;
                case CraftingDecision.Currency:
                    record.Bucket = TerminalBucket.CurrencyLeaf;
                    break;
                case CraftingDecision.Craft:
                    // A Craft decision with no children: the recipe expanded
                    // to nothing, so it is a terminal despite the decision.
                    record.Bucket = TerminalBucket.OtherUnpriced;
                    record.Badge = "CRAFT-NO-CHILDREN";
                    break;
                default:
                    record.Bucket = TerminalBucket.OtherUnpriced;
                    record.Badge = node.Decision.ToString();
                    break;
            }

            return record;
        }

        private static void ClassifyVendor(
            CraftingTreeNode node, CurrencyValuation valuation, TerminalRecord record)
        {
            var lines = node.VendorCurrencyCosts;
            if ((lines == null || lines.Count == 0) && !node.VendorHasBarterItemCost)
            {
                record.Bucket = TerminalBucket.VendorCoin;
                return;
            }

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    bool valued;
                    if (string.Equals(line.Type, "Item", StringComparison.Ordinal))
                    {
                        valued = valuation.TryGetEffectiveItemCopperValue(line.Id, out _);
                    }
                    else
                    {
                        valued = valuation.TryGetEffectiveCopperValue(line.Id, out _);
                    }

                    if (!valued)
                    {
                        record.UnvaluedLines.Add(line.Type + ":" + line.Id.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            // VendorHasBarterItemCost means an untradeable barter item with
            // no gold value reached the winning offer; the item ids behind
            // it are not exposed on the tree node, so it is recorded as one
            // opaque unvalued line rather than being dropped.
            if (node.VendorHasBarterItemCost)
            {
                // The barter ITEM ids are not on the node itself; the only
                // place they surface is the display-only cost-component
                // leaves CraftingTreeBuilder synthesizes underneath it.
                // Naming them is the whole point of this run, so read them
                // back off the children rather than reporting "an item".
                var named = node.Children
                    .Where(child => child.IsCostComponent)
                    .Select(child => "CostComponent:" + child.ItemId.ToString(CultureInfo.InvariantCulture) +
                        " (" + (string.IsNullOrEmpty(child.Name) ? "?" : child.Name) + " x" +
                        child.Quantity.ToString(CultureInfo.InvariantCulture) + ")")
                    .ToList();
                if (named.Count == 0)
                {
                    named.Add("BarterItem:unnamed (offer had a single cost kind, no component leaves)");
                }

                record.UnvaluedLines.AddRange(named);
            }

            record.Bucket = record.UnvaluedLines.Count > 0
                ? TerminalBucket.VendorUnvalued
                : TerminalBucket.VendorValuedCurrency;
        }

        private static void PrintPerItemTable(List<ItemClassification> all, string mode)
        {
            Console.WriteLine($"=== Terminal classification [{mode}] ===");
            Console.WriteLine();
            Console.WriteLine("| Item | id | planned | root decision | forced | nodes | depth | ms | root recipes | 1 TP | 2 vendor coin | 3 vendor valued | 4 vendor unvalued | 5 unknown | have | currency | other | terminals |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var c in all)
            {
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {16} | {17} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} | {11} | {12} | {13} | {14} | {15} |",
                    c.Name,
                    c.ItemId,
                    c.Planned ? "yes" : "NO (" + c.Error + ")",
                    c.PlanNodeCount,
                    c.PlanDepth,
                    c.SolveMs,
                    c.RootRecipeCount,
                    c.Count(TerminalBucket.TradingPost),
                    c.Count(TerminalBucket.VendorCoin),
                    c.Count(TerminalBucket.VendorValuedCurrency),
                    c.Count(TerminalBucket.VendorUnvalued),
                    c.Count(TerminalBucket.Unknown),
                    c.Count(TerminalBucket.Have),
                    c.Count(TerminalBucket.CurrencyLeaf),
                    c.Count(TerminalBucket.OtherUnpriced),
                    c.Terminals.Count,
                    c.Planned ? c.RootDecision.ToString() : "-",
                    c.RootForcedToCraft ? "yes" : "no"));
            }
        }

        private static void PrintPerItemDetail(List<ItemClassification> all)
        {
            Console.WriteLine("=== Unpriceable terminals per item (buckets 4 and 5) ===");
            foreach (var c in all)
            {
                Console.WriteLine();
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "-- {0} ({1}) nameResolved={2} probabilisticForgeOutputs={3}",
                    c.Name, c.ItemId, c.RootNameResolved, c.ProbabilisticForgeOutputs));
                if (!c.Planned)
                {
                    Console.WriteLine("   NOT PLANNED: " + c.Error);
                    continue;
                }

                var blockers = c.Terminals
                    .Where(IsBlocker)
                    .GroupBy(t => t.ItemId)
                    .Select(g => new
                    {
                        Id = g.Key,
                        g.First().Name,
                        g.First().Bucket,
                        Qty = g.Sum(t => t.Quantity),
                        Occurrences = g.Count(),
                        Badge = g.First().Badge,
                        Lines = string.Join(",", g.SelectMany(t => t.UnvaluedLines).Distinct()),
                    })
                    .OrderByDescending(x => x.Qty)
                    .ToList();

                if (blockers.Count == 0)
                {
                    Console.WriteLine("   (none - every terminal is priced)");
                    continue;
                }

                foreach (var b in blockers)
                {
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "   [{0}] {1} ({2}) qty={3} x{4} badge={5} unvalued={6}",
                        b.Bucket, b.Name, b.Id, b.Qty, b.Occurrences,
                        string.IsNullOrEmpty(b.Badge) ? "-" : b.Badge,
                        string.IsNullOrEmpty(b.Lines) ? "-" : b.Lines));
                }
            }
        }

        /// <summary>
        /// Every terminal item that appears in 2+ trees, whatever bucket it
        /// landed in. The blocker table above only lists what the module
        /// cannot price; this one exists to CONFIRM OR REFUTE a prior guess
        /// about which shared components matter, including the ones that
        /// turn out to be priced perfectly well.
        /// </summary>
        private static void PrintRecurringAllBuckets(List<ItemClassification> planned)
        {
            Console.WriteLine("=== Recurring terminals, ALL buckets (2+ trees) ===");
            Console.WriteLine();
            Console.WriteLine("| Terminal item | id | trees | total qty | buckets |");
            Console.WriteLine("|---|---|---|---|---|");

            var trees = new Dictionary<int, HashSet<string>>();
            var names = new Dictionary<int, string>();
            var quantities = new Dictionary<int, long>();
            var buckets = new Dictionary<int, SortedSet<string>>();

            foreach (var c in planned)
            {
                foreach (var t in c.Terminals)
                {
                    if (!trees.TryGetValue(t.ItemId, out var set))
                    {
                        set = new HashSet<string>();
                        trees[t.ItemId] = set;
                        names[t.ItemId] = t.Name;
                        quantities[t.ItemId] = 0L;
                        buckets[t.ItemId] = new SortedSet<string>();
                    }

                    set.Add(c.Name);
                    quantities[t.ItemId] += t.Quantity;
                    buckets[t.ItemId].Add(t.Bucket.ToString());
                }
            }

            foreach (var entry in trees
                .Where(e => e.Value.Count >= 2)
                .OrderByDescending(e => e.Value.Count)
                .ThenByDescending(e => quantities[e.Key]))
            {
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3} | {4} |",
                    names[entry.Key], entry.Key, entry.Value.Count,
                    quantities[entry.Key], string.Join("+", buckets[entry.Key])));
            }
        }

        private static void PrintAggregate(List<ItemClassification> all)
        {
            var planned = all.Where(c => c.Planned).ToList();

            Console.WriteLine("=== Aggregate ===");
            Console.WriteLine();
            Console.WriteLine("| Bucket | Terminals | Share of terminals |");
            Console.WriteLine("|---|---|---|");
            int total = planned.Sum(c => c.Terminals.Count);
            foreach (TerminalBucket bucket in Enum.GetValues(typeof(TerminalBucket)))
            {
                int n = planned.Sum(c => c.Count(bucket));
                double pct = total > 0 ? (double)n / total * 100.0 : 0.0;
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2:F1}% |", bucket, n, pct));
            }

            Console.WriteLine();
            Console.WriteLine("=== Recurring blockers (bucket 4/5/other, ranked by number of trees) ===");
            Console.WriteLine();
            Console.WriteLine("| Blocking item | id | trees | total qty across trees | bucket | badge |");
            Console.WriteLine("|---|---|---|---|---|---|");

            var byItem = new Dictionary<int, List<Tuple<string, TerminalRecord, int>>>();
            foreach (var c in planned)
            {
                foreach (var group in c.Terminals
                    .Where(IsBlocker)
                    .GroupBy(t => t.ItemId))
                {
                    if (!byItem.TryGetValue(group.Key, out var list))
                    {
                        list = new List<Tuple<string, TerminalRecord, int>>();
                        byItem[group.Key] = list;
                    }

                    list.Add(Tuple.Create(c.Name, group.First(), group.Sum(t => t.Quantity)));
                }
            }

            foreach (var entry in byItem
                .OrderByDescending(e => e.Value.Count)
                .ThenByDescending(e => e.Value.Sum(v => v.Item3)))
            {
                var sample = entry.Value[0].Item2;
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3} | {4} | {5} |",
                    sample.Name,
                    entry.Key,
                    entry.Value.Count,
                    entry.Value.Sum(v => v.Item3),
                    sample.Bucket,
                    string.IsNullOrEmpty(sample.Badge) ? "-" : sample.Badge));
            }

            Console.WriteLine();
            Console.WriteLine("=== Vendor terminals whose coin cost is zero ===");
            Console.WriteLine();
            Console.WriteLine("| Item | plan gold total (copper) | vendor terminals | of which zero-coin |");
            Console.WriteLine("|---|---|---|---|");
            foreach (var c in planned)
            {
                var vendorTerminals = c.Terminals
                    .Where(t => t.Bucket == TerminalBucket.VendorCoin ||
                                t.Bucket == TerminalBucket.VendorValuedCurrency ||
                                t.Bucket == TerminalBucket.VendorUnvalued)
                    .ToList();
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3} |",
                    c.Name,
                    c.RootSubtreeCost.HasValue
                        ? c.RootSubtreeCost.Value.ToString(CultureInfo.InvariantCulture)
                        : "-",
                    vendorTerminals.Count,
                    vendorTerminals.Count(t => t.ZeroCoin)));
            }

            Console.WriteLine();
            Console.WriteLine("=== Non-coin cost charged across all trees (never in any gold total) ===");
            Console.WriteLine();
            Console.WriteLine("| Kind | id | units charged | trees |");
            Console.WriteLine("|---|---|---|---|");
            var lineTotals = new Dictionary<string, long>();
            var lineTrees = new Dictionary<string, HashSet<string>>();
            foreach (var c in planned)
            {
                foreach (var t in c.Terminals)
                {
                    foreach (var line in t.CostLines)
                    {
                        string key = line.Type + "|" + line.Id.ToString(CultureInfo.InvariantCulture);
                        lineTotals.TryGetValue(key, out long running);
                        // CostLine.Count is already scaled to the node's own
                        // demand by VendorBatchSolver's ScaleCostLines - do
                        // NOT multiply by Quantity again.
                        lineTotals[key] = running + line.Count;
                        if (!lineTrees.TryGetValue(key, out var set))
                        {
                            set = new HashSet<string>();
                            lineTrees[key] = set;
                        }

                        set.Add(c.Name);
                    }
                }
            }

            foreach (var entry in lineTotals.OrderByDescending(e => lineTrees[e.Key].Count).ThenByDescending(e => e.Value))
            {
                string[] parts = entry.Key.Split('|');
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3} |",
                    parts[0], parts[1], entry.Value, lineTrees[entry.Key].Count));
            }

            Console.WriteLine();
            PrintRecurringAllBuckets(planned);

            Console.WriteLine();
            Console.WriteLine("Trees analysed: " + planned.Count.ToString(CultureInfo.InvariantCulture) +
                " of " + all.Count.ToString(CultureInfo.InvariantCulture));
        }
    }
}
