using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TaimisToolbench.Services.Diagnostics
{
    internal static class PlanTimingAnalyzer
    {
        // Extracted so PlanPhaseTimingSummary can locate exactly where
        // the raw per-step timing lines end within a full DebugLog (raw
        // lines, then this marker, then the percentage summary below) -
        // previously only an inline literal here.
        public const string SummaryHeaderLine = "--- Timing Summary ---";

        public class ParsedPhase
        {
            public string Name { get; set; }

            public long ElapsedMs { get; set; }

            public int? Count { get; set; }
        }

        private static readonly Regex TimingLinePattern =
            new Regex(@"^(.+?):\s*(\d+)ms(?:\s*\((\d+)\s+items?\))?$", RegexOptions.Compiled);

        public static List<ParsedPhase> Parse(IReadOnlyList<string> timingLines)
        {
            if (timingLines == null)
            {
                return new List<ParsedPhase>();
            }

            var results = new List<ParsedPhase>();
            foreach (var line in timingLines)
            {
                if (line == null)
                {
                    continue;
                }

                var match = TimingLinePattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var phase = new ParsedPhase
                {
                    Name = match.Groups[1].Value,
                    ElapsedMs = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                };

                if (match.Groups[3].Success)
                {
                    phase.Count = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                }

                results.Add(phase);
            }

            return results;
        }

        public static List<string> Summarize(IReadOnlyList<string> timingLines)
        {
            var phases = Parse(timingLines);
            if (phases.Count == 0)
            {
                return new List<string>();
            }

            long total = phases.Sum(p => p.ElapsedMs);
            var sorted = phases.OrderByDescending(p => p.ElapsedMs).ToList();

            var summary = new List<string>
            {
                SummaryHeaderLine,
                string.Format(CultureInfo.InvariantCulture, "Total: {0}ms", total),
            };

            foreach (var phase in sorted)
            {
                double pct = total > 0
                    ? (double)phase.ElapsedMs / total * 100.0
                    : 0.0;
                summary.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1}ms ({2:F1}%)",
                    phase.Name,
                    phase.ElapsedMs,
                    pct));
            }

            return summary;
        }
    }
}
