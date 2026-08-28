using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Services.Diagnostics;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class PlanTimingAnalyzerTests
    {
        [Fact]
        public void Parse_ValidLines_ReturnsStructured()
        {
            var lines = new List<string>
            {
                "Build tree: 42ms",
                "Fetch prices: 150ms (10 items)",
            };

            var parsed = PlanTimingAnalyzer.Parse(lines);

            Assert.Equal(2, parsed.Count);

            Assert.Equal("Build tree", parsed[0].Name);
            Assert.Equal(42, parsed[0].ElapsedMs);
            Assert.Null(parsed[0].Count);

            Assert.Equal("Fetch prices", parsed[1].Name);
            Assert.Equal(150, parsed[1].ElapsedMs);
            Assert.Equal(10, parsed[1].Count);
        }

        [Fact]
        public void Parse_NoMatchingLines_ReturnsEmpty()
        {
            var lines = new List<string>
            {
                "no match",
                "--- Timing Summary ---",
                "Required disciplines: Weaponsmith",
            };

            var parsed = PlanTimingAnalyzer.Parse(lines);

            Assert.Empty(parsed);
        }

        [Fact]
        public void Summarize_ProducesSortedSummary()
        {
            var lines = new List<string>
            {
                "Phase A: 100ms",
                "Phase B: 300ms",
                "Phase C: 200ms",
            };

            var summary = PlanTimingAnalyzer.Summarize(lines);

            // Header
            Assert.True(summary.Count >= 5,
                $"Expected >= 5 summary lines, got {summary.Count}");
            Assert.Equal("--- Timing Summary ---", summary[0]);

            // Total
            Assert.StartsWith("Total:", summary[1]);
            Assert.Contains("600ms", summary[1]);

            // Phases sorted descending by ms
            Assert.Contains("Phase B", summary[2]);
            Assert.Contains("300ms", summary[2]);
            Assert.Contains("50.0%", summary[2]);

            Assert.Contains("Phase C", summary[3]);
            Assert.Contains("200ms", summary[3]);
            Assert.Contains("33.3%", summary[3]);

            Assert.Contains("Phase A", summary[4]);
            Assert.Contains("100ms", summary[4]);
            Assert.Contains("16.7%", summary[4]);
        }

        [Fact]
        public void Summarize_EmptyInput_ReturnsEmpty()
        {
            var summary = PlanTimingAnalyzer.Summarize(new List<string>());

            Assert.Empty(summary);
        }
    }
}
