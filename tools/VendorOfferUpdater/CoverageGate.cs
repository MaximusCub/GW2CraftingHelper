using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VendorOfferUpdater.Models;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Decides whether a freshly scraped dataset may overwrite the one on
    /// disk. The merge step's data-loss guard only protects rows already in
    /// the baseline, so nothing detected rows a run never fetched at all: a
    /// refused branch of the vendor namespace produced a smaller file and no
    /// complaint. This compares the two datasets and refuses to write when
    /// the run left a section unresolved or lost a large share of its rows.
    /// </summary>
    internal static class CoverageGate
    {
        /// <summary>
        /// Share of merchants or offers a run may lose before the write is
        /// blocked. A real refresh moves individual rows; it does not move
        /// one row in fifty.
        /// </summary>
        internal const double DefaultMaxDropFraction = 0.02;

        /// <summary>Cap on merchants named in the report. The count is exact.</summary>
        internal const int MaxListedMerchants = 20;

        internal sealed class Report
        {
            public int OldOfferCount { get; set; }

            public int NewOfferCount { get; set; }

            public int OldMerchantCount { get; set; }

            public int NewMerchantCount { get; set; }

            public int UnresolvedCount { get; set; }

            /// <summary>Merchants in the old dataset and not in the new one.</summary>
            public List<string> MerchantsLost { get; } = new List<string>();

            public List<string> Reasons { get; } = new List<string>();

            /// <summary>Set when reasons exist but the override was passed.</summary>
            public bool Overridden { get; set; }

            public bool Blocked => Reasons.Count > 0 && !Overridden;
        }

        /// <summary>
        /// Compares the dataset about to be written against the one it would
        /// replace. A null or empty previous dataset is a first run: there is
        /// nothing to compare, so only unresolved sections can block it.
        /// </summary>
        internal static Report Evaluate(
            IReadOnlyList<VendorOffer>? previous,
            IReadOnlyList<VendorOffer>? next,
            IReadOnlyList<UnresolvedSection>? unresolved,
            double maxDropFraction,
            bool overridden)
        {
            previous ??= Array.Empty<VendorOffer>();
            next ??= Array.Empty<VendorOffer>();
            unresolved ??= Array.Empty<UnresolvedSection>();

            var diff = VendorOfferDiff.Compute(previous, next);

            var oldMerchants = Merchants(previous);
            var newMerchants = Merchants(next);

            var report = new Report
            {
                OldOfferCount = diff.OldCount,
                NewOfferCount = diff.NewCount,
                OldMerchantCount = oldMerchants.Count,
                NewMerchantCount = newMerchants.Count,
                UnresolvedCount = unresolved.Count,
                Overridden = overridden,
            };

            foreach (var merchant in oldMerchants.Where(m => !newMerchants.Contains(m))
                                                 .OrderBy(m => m, StringComparer.Ordinal))
            {
                report.MerchantsLost.Add(merchant);
            }

            if (unresolved.Count > 0)
            {
                report.Reasons.Add(
                    $"{unresolved.Count} section(s) went unresolved, so this dataset is "
                    + "missing rows the wiki was never able to answer for.");
            }

            AddDropReason(report, "offers", diff.OldCount, diff.NewCount, maxDropFraction);
            AddDropReason(
                report, "distinct merchants", oldMerchants.Count, newMerchants.Count, maxDropFraction);

            // Reasons that exist but were overridden are still reported; the
            // override decides whether they stop the write, not whether the
            // run admits to them.
            if (report.Reasons.Count == 0)
            {
                report.Overridden = false;
            }

            return report;
        }

        internal static string Format(Report report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Coverage check ===");
            sb.Append("  offers:    ").AppendLine(Movement(report.OldOfferCount, report.NewOfferCount));
            sb.Append("  merchants: ")
              .AppendLine(Movement(report.OldMerchantCount, report.NewMerchantCount));
            sb.Append("  unresolved sections: ")
              .AppendLine(report.UnresolvedCount.ToString(CultureInfo.InvariantCulture));

            if (report.MerchantsLost.Count > 0)
            {
                sb.Append("  merchants no longer present (")
                  .Append(report.MerchantsLost.Count.ToString(CultureInfo.InvariantCulture))
                  .AppendLine("):");
                foreach (var merchant in report.MerchantsLost.Take(MaxListedMerchants))
                {
                    sb.Append("    - ").AppendLine(merchant);
                }

                if (report.MerchantsLost.Count > MaxListedMerchants)
                {
                    sb.Append("    ... and ")
                      .Append((report.MerchantsLost.Count - MaxListedMerchants)
                          .ToString(CultureInfo.InvariantCulture))
                      .AppendLine(" more");
                }
            }

            if (report.Reasons.Count == 0)
            {
                sb.AppendLine("  PASS: coverage held.");
                return sb.ToString();
            }

            sb.AppendLine(report.Overridden
                ? "  OVERRIDDEN: writing anyway, --allow-coverage-drop was passed."
                : "  BLOCKED: the dataset was NOT written.");
            foreach (var reason in report.Reasons)
            {
                sb.Append("    - ").AppendLine(reason);
            }

            if (!report.Overridden)
            {
                sb.AppendLine(
                    "  Re-run the unresolved sections (see the sidecar file named above),"
                    + " or pass --allow-coverage-drop if the loss is intended.");
            }

            return sb.ToString();
        }

        private static void AddDropReason(
            Report report, string what, int oldCount, int newCount, double maxDropFraction)
        {
            if (oldCount <= 0 || newCount >= oldCount)
            {
                return;
            }

            double dropped = (oldCount - newCount) / (double)oldCount;
            if (dropped <= maxDropFraction)
            {
                return;
            }

            report.Reasons.Add(
                $"{what} fell from {oldCount.ToString("N0", CultureInfo.InvariantCulture)} to "
                + $"{newCount.ToString("N0", CultureInfo.InvariantCulture)} "
                + $"({dropped * 100:F1}% lost), past the "
                + $"{maxDropFraction * 100:F1}% threshold.");
        }

        private static HashSet<string> Merchants(IReadOnlyList<VendorOffer> offers)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var offer in offers)
            {
                names.Add(offer.MerchantName ?? string.Empty);
            }

            return names;
        }

        private static string Movement(int oldCount, int newCount)
        {
            int delta = newCount - oldCount;
            return $"{oldCount.ToString("N0", CultureInfo.InvariantCulture)} -> "
                 + $"{newCount.ToString("N0", CultureInfo.InvariantCulture)} "
                 + $"({(delta >= 0 ? "+" : "-")}{Math.Abs(delta).ToString("N0", CultureInfo.InvariantCulture)})";
        }
    }

    /// <summary>
    /// The sidecar naming the sections a run could not resolve. It exists so
    /// a follow-up run can re-target those queries alone: each entry carries
    /// the exact condition that failed, which turns a two thousand request
    /// recovery into a handful.
    /// </summary>
    internal static class UnresolvedSectionFile
    {
        internal static string PathFor(string datasetPath)
        {
            string? dir = Path.GetDirectoryName(datasetPath);
            string name = Path.GetFileNameWithoutExtension(datasetPath) + "_unresolved.json";
            return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
        }

        internal static string Serialize(IReadOnlyList<UnresolvedSection> sections)
        {
            var document = new UnresolvedSectionDocument
            {
                GeneratedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Sections = sections.ToList(),
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };

            return JsonSerializer.Serialize(document, jsonOptions).Replace("\r\n", "\n") + "\n";
        }

        /// <summary>
        /// Writes the sidecar, or deletes a previous run's when this run left
        /// nothing unresolved. A stale file would name sections that no longer
        /// need re-targeting.
        /// </summary>
        internal static async Task<string?> SaveAsync(
            string datasetPath, IReadOnlyList<UnresolvedSection> sections)
        {
            string path = PathFor(datasetPath);

            if (sections.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return null;
            }

            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(path, Serialize(sections));
            return path;
        }
    }

    internal sealed class UnresolvedSectionDocument
    {
        public string GeneratedAt { get; set; } = string.Empty;

        public List<UnresolvedSection> Sections { get; set; } = new List<UnresolvedSection>();
    }
}
