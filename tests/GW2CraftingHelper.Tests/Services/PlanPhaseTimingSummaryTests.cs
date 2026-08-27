using System.Collections.Generic;
using TaimisToolbench.Services.Diagnostics;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class PlanPhaseTimingSummaryTests
    {
        [Fact]
        public void FormatCompactSummary_NullDebugLog_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, PlanPhaseTimingSummary.FormatCompactSummary(null));
        }

        [Fact]
        public void FormatCompactSummary_EmptyDebugLog_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, PlanPhaseTimingSummary.FormatCompactSummary(new List<string>()));
        }

        [Fact]
        public void FormatCompactSummary_NoRecognizableTimingLines_ReturnsEmpty()
        {
            var debugLog = new List<string> { "No inventory reduction (snapshot not provided)" };
            Assert.Equal(string.Empty, PlanPhaseTimingSummary.FormatCompactSummary(debugLog));
        }

        // Mirrors the exact shape CraftingPlanPipeline.FinishTimingLog
        // produces for a single-item plan: raw timing lines, then
        // PlanTimingAnalyzer.SummaryHeaderLine, then the percentage
        // summary, then (in a real result) PlanResultBuilder's own
        // reduction/decision debug lines.
        private static List<string> BuildRealisticSingleItemDebugLog()
        {
            return new List<string>
            {
                "Build recipe tree: 120ms",
                "Collect item IDs: 5ms (3 items)",
                "Fetch TP prices: 8400ms (3 items)",
                "Query vendor offers: 2ms",
                "Inventory reduction: 1ms",
                "Solve: 30ms",
                "Fetch item metadata: 9200ms (3 items)",
                "Fetch currency metadata: 50ms",
                "Fetch learned recipes: 100ms",
                "Build result: 250ms",
                PlanTimingAnalyzer.SummaryHeaderLine,
                "Total: 18158ms",
                "Fetch item metadata: 9200ms (50.7%)",
                "Fetch TP prices: 8400ms (46.3%)",
                "Build result: 250ms (1.4%)",
                "Solve: 30ms (0.2%)",
                "Build recipe tree: 120ms (0.7%)",
                "Fetch learned recipes: 100ms (0.6%)",
                "Fetch currency metadata: 50ms (0.3%)",
                "Collect item IDs: 5ms (0.0%)",
                "Query vendor offers: 2ms (0.0%)",
                "Inventory reduction: 1ms (0.0%)",
                // PlanResultBuilder's own trailing debug lines - must never
                // be mistaken for a timing line even though this one is
                // colon-shaped.
                "No inventory reduction (snapshot not provided)",
                "Recipe permission not available",
            };
        }

        [Fact]
        public void FormatCompactSummary_SingleItemDebugLog_BucketsIntoPhasesInOrder()
        {
            string summary = PlanPhaseTimingSummary.FormatCompactSummary(BuildRealisticSingleItemDebugLog());

            // tree = 120 + 5 = 125; prices = 8400 + 2 = 8402;
            // solve = 1 + 30 = 31; item details = 9200 + 50 = 9250;
            // learned recipes = 100; display = 250;
            // total = 125+8402+31+9250+100+250 = 18158.
            Assert.Equal(
                "tree 125ms, prices 8402ms (3 items), solve 31ms, item details 9250ms (3 items), learned recipes 100ms, display 250ms - total 18158ms",
                summary);
        }

        [Fact]
        public void FormatCompactSummary_LearnedRecipes_DoesNotInflateTheItemDetailsBucket()
        {
            // The account round trip used to be added to "item details",
            // which is annotated with the item-metadata count - so a 4.5s
            // /v2/account/recipes call was reported as "item details
            // (3 items)". The two must stay separately attributable.
            var debugLog = new List<string>
            {
                "Fetch item metadata: 5ms (3 items)",
                "Fetch learned recipes: 4557ms",
            };

            string summary = PlanPhaseTimingSummary.FormatCompactSummary(debugLog);

            Assert.Equal(
                "item details 5ms (3 items), learned recipes 4557ms - total 4562ms",
                summary);
        }

        [Fact]
        public void FormatCompactSummary_WithWallClockMs_UsesWallClockAsTotalAndAppendsPhaseSum()
        {
            // The phase-sum-only "total" this used to log
            // silently under-reports the real duration a field tester
            // experiences by however long the un-instrumented gaps between
            // steps ran. When the caller supplies the wrapper's own
            // wall-clock elapsed time, it becomes the "total", with the
            // phase sum appended alongside for diagnostic comparison - see
            // the class doc comment's own worked example.
            string summary = PlanPhaseTimingSummary.FormatCompactSummary(
                BuildRealisticSingleItemDebugLog(), wallClockMs: 19036);

            Assert.Equal(
                "tree 125ms, prices 8402ms (3 items), solve 31ms, item details 9250ms (3 items), learned recipes 100ms, display 250ms - total 19036ms (phases 18158ms)",
                summary);
        }

        [Fact]
        public void FormatCompactSummary_NullWallClockMs_KeepsPhaseSumOnlyTotal_BackwardCompatible()
        {
            // The default (omitted) parameter must reproduce the exact
            // pre-existing wording - every current caller/test relies on
            // this (see FormatCompactSummary_SingleItemDebugLog_
            // BucketsIntoPhasesInOrder above).
            string summary = PlanPhaseTimingSummary.FormatCompactSummary(
                BuildRealisticSingleItemDebugLog(), wallClockMs: null);

            Assert.EndsWith(" - total 18158ms", summary);
            Assert.DoesNotContain("phases", summary);
        }

        [Fact]
        public void FormatCompactSummary_StopsAtSummaryHeaderMarker_TrailingLinesNeverDoubleCounted()
        {
            // The "Total: 18158ms" line inside the percentage summary block
            // itself matches the same "Name: NNNms" shape a raw timing line
            // does - if FormatCompactSummary did not stop at the marker, it
            // would double-count every bucket and add a spurious unbucketed
            // "Total" entry to the grand total.
            var withTrailingBlock = BuildRealisticSingleItemDebugLog();
            var rawOnly = withTrailingBlock.GetRange(0, 10); // just the 10 raw timing lines

            Assert.Equal(
                PlanPhaseTimingSummary.FormatCompactSummary(rawOnly),
                PlanPhaseTimingSummary.FormatCompactSummary(withTrailingBlock));
        }

        [Fact]
        public void FormatCompactSummary_MultiItemPluralTreeLine_BucketsUnderTree()
        {
            var debugLog = new List<string>
            {
                "Build recipe trees: 300ms (2 items)",
                "Collect item IDs: 10ms (5 items)",
                "Fetch TP prices: 400ms (5 items)",
                "Solve: 20ms",
                "Fetch item metadata: 500ms (5 items)",
                "Build result: 40ms",
            };

            string summary = PlanPhaseTimingSummary.FormatCompactSummary(debugLog);

            Assert.StartsWith("tree 310ms, ", summary);
            Assert.EndsWith(" - total 1270ms", summary);
        }

        [Fact]
        public void FormatCompactSummary_UnrecognizedStepName_CountsTowardTotalButNotBucketed()
        {
            var debugLog = new List<string>
            {
                "Build recipe tree: 100ms",
                "Some Future Step: 50ms",
            };

            string summary = PlanPhaseTimingSummary.FormatCompactSummary(debugLog);

            // Only the recognized "tree" bucket appears, but the total
            // still reflects the unrecognized step's 50ms too.
            Assert.Equal("tree 100ms - total 150ms", summary);
        }

        [Fact]
        public void FormatCompactSummary_MissingPhase_OmittedFromOutputNotCrashed()
        {
            // A plan with no priced items at all could plausibly skip a
            // bucket in a hypothetical future pipeline shape - the
            // formatter must degrade gracefully (skip it) rather than throw.
            var debugLog = new List<string>
            {
                "Build recipe tree: 10ms",
                "Build result: 5ms",
            };

            string summary = PlanPhaseTimingSummary.FormatCompactSummary(debugLog);

            Assert.Equal("tree 10ms, display 5ms - total 15ms", summary);
        }
    }
}
