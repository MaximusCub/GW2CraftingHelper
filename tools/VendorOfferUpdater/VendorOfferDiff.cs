using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VendorOfferUpdater.Models;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Answers the one question a reviewer of a `data(vendor):` commit cannot
    /// otherwise answer: what actually changed. `git diff` on
    /// ref/vendor_offers.json reports "1 insertion(+), 1 deletion(-)" on a
    /// 14.8MB single line, which is the entire dataset replaced as one
    /// indivisible hunk.
    /// <para>
    /// The naive answer - list the offerIds that appeared and disappeared - is
    /// almost as useless, because offerId is a SHA-256 over the offer's whole
    /// content. Change one price and the row does not "change": it vanishes and
    /// a different hash appears. So a raw added/removed pair list turns every
    /// repricing into two unrelated-looking hex strings.
    /// </para>
    /// <para>
    /// This re-pairs those by (merchant, output item), which the hash does not
    /// preserve but a human reads instantly, and reports them as repricings with
    /// the old and new cost side by side. Only rows with no counterpart are
    /// reported as genuine additions or removals. SeasonalFestival is the one
    /// field outside the hash, so a change to it keeps the offerId and is
    /// reported separately as a retag. A row whose id is shared but whose
    /// content is not - a hand-edit, or a row predating a hash-format change -
    /// is reported as a repricing rather than trusted on its id alone.
    /// </para>
    /// </summary>
    internal static class VendorOfferDiff
    {
        /// <summary>Cap on rows listed per section. Counts are always exact.</summary>
        internal const int MaxListedPerSection = 50;

        internal sealed class Result
        {
            public int OldCount { get; set; }

            public int NewCount { get; set; }

            public List<VendorOffer> Added { get; } = new List<VendorOffer>();

            public List<VendorOffer> Removed { get; } = new List<VendorOffer>();

            public List<OfferChange> Repriced { get; } = new List<OfferChange>();

            public List<OfferChange> Retagged { get; } = new List<OfferChange>();

            public bool IsEmpty =>
                Added.Count == 0
                && Removed.Count == 0
                && Repriced.Count == 0
                && Retagged.Count == 0;
        }

        internal sealed class OfferChange
        {
            public OfferChange(VendorOffer before, VendorOffer after)
            {
                Before = before;
                After = after;
            }

            public VendorOffer Before { get; }

            public VendorOffer After { get; }
        }

        private readonly struct PairKey : IEquatable<PairKey>
        {
            public PairKey(VendorOffer offer)
            {
                Merchant = offer.MerchantName ?? string.Empty;
                OutputItemId = offer.OutputItemId;
            }

            public string Merchant { get; }

            public int OutputItemId { get; }

            public bool Equals(PairKey other) =>
                OutputItemId == other.OutputItemId
                && string.Equals(Merchant, other.Merchant, StringComparison.Ordinal);

            public override bool Equals(object? obj) => obj is PairKey other && Equals(other);

            public override int GetHashCode() =>
                (StringComparer.Ordinal.GetHashCode(Merchant) * 397) ^ OutputItemId;
        }

        internal static Result Compute(
            IReadOnlyList<VendorOffer>? before,
            IReadOnlyList<VendorOffer>? after)
        {
            before ??= Array.Empty<VendorOffer>();
            after ??= Array.Empty<VendorOffer>();

            var result = new Result
            {
                OldCount = before.Count,
                NewCount = after.Count,
            };

            var beforeById = IndexById(before);
            var afterById = IndexById(after);

            // A shared offerId is only EVIDENCE that the content matched, not
            // proof: the id is a hash the tool computes, and a hand-edited row
            // (or one predating a hash-format change) can carry an id that no
            // longer describes it. Compare the content anyway - reporting such
            // a row as unchanged would make this report lie in exactly the case
            // a reviewer most needs it not to.
            foreach (var kvp in beforeById.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!afterById.TryGetValue(kvp.Key, out var afterOffer))
                {
                    continue;
                }

                if (!string.Equals(
                        Program.ComputeContentKey(kvp.Value),
                        Program.ComputeContentKey(afterOffer),
                        StringComparison.Ordinal))
                {
                    result.Repriced.Add(new OfferChange(kvp.Value, afterOffer));
                }
                else if (!string.Equals(
                        kvp.Value.SeasonalFestival,
                        afterOffer.SeasonalFestival,
                        StringComparison.Ordinal))
                {
                    result.Retagged.Add(new OfferChange(kvp.Value, afterOffer));
                }
            }

            var vanished = before
                .Where(o => o.OfferId == null || !afterById.ContainsKey(o.OfferId))
                .ToList();
            var appeared = after
                .Where(o => o.OfferId == null || !beforeById.ContainsKey(o.OfferId))
                .ToList();

            var vanishedByPair = GroupByPair(vanished);
            var appearedByPair = GroupByPair(appeared);

            foreach (var pair in AllPairs(vanishedByPair, appearedByPair))
            {
                vanishedByPair.TryGetValue(pair, out var gone);
                appearedByPair.TryGetValue(pair, out var came);
                gone ??= new List<VendorOffer>();
                came ??= new List<VendorOffer>();

                // Where a merchant sells the same item on several rows, the
                // pairing between them is arbitrary. Pair positionally and let
                // the surplus fall through as real additions/removals: the
                // counts stay exact either way, and the reviewer still sees
                // every changed row's before and after.
                int paired = Math.Min(gone.Count, came.Count);
                for (int i = 0; i < paired; i++)
                {
                    result.Repriced.Add(new OfferChange(gone[i], came[i]));
                }

                result.Removed.AddRange(gone.Skip(paired));
                result.Added.AddRange(came.Skip(paired));
            }

            return result;
        }

        internal static string Format(Result result, string beforeLabel, string afterLabel)
        {
            var sb = new StringBuilder();
            int delta = result.NewCount - result.OldCount;

            sb.Append("=== Vendor offer diff: ").Append(beforeLabel)
              .Append(" -> ").Append(afterLabel).AppendLine(" ===");
            sb.Append("  offers:   ").Append(result.OldCount.ToString("N0", CultureInfo.InvariantCulture))
              .Append(" -> ").Append(result.NewCount.ToString("N0", CultureInfo.InvariantCulture))
              .Append(" (").Append(delta >= 0 ? "+" : "-")
              .Append(Math.Abs(delta).ToString("N0", CultureInfo.InvariantCulture))
              .AppendLine(")");
            sb.Append("  added:    ").AppendLine(result.Added.Count.ToString(CultureInfo.InvariantCulture));
            sb.Append("  removed:  ").AppendLine(result.Removed.Count.ToString(CultureInfo.InvariantCulture));
            sb.Append("  repriced: ").AppendLine(result.Repriced.Count.ToString(CultureInfo.InvariantCulture));
            sb.Append("  retagged: ").AppendLine(result.Retagged.Count.ToString(CultureInfo.InvariantCulture));

            if (result.IsEmpty)
            {
                sb.AppendLine();
                sb.AppendLine("  No offer changed. The datasets are equivalent.");
                return sb.ToString();
            }

            AppendSection(sb, "Added", result.Added, Describe);
            AppendSection(sb, "Removed", result.Removed, Describe);
            AppendSection(sb, "Repriced", result.Repriced, DescribeReprice);
            AppendSection(sb, "Retagged", result.Retagged, DescribeRetag);

            return sb.ToString();
        }

        private static void AppendSection<T>(
            StringBuilder sb, string title, List<T> rows, Func<T, string> describe)
        {
            if (rows.Count == 0)
            {
                return;
            }

            sb.AppendLine();
            sb.Append("--- ").Append(title).Append(" (")
              .Append(rows.Count.ToString(CultureInfo.InvariantCulture)).AppendLine(") ---");

            foreach (var row in rows.Take(MaxListedPerSection))
            {
                sb.Append("  ").AppendLine(describe(row));
            }

            if (rows.Count > MaxListedPerSection)
            {
                sb.Append("  ... and ")
                  .Append((rows.Count - MaxListedPerSection).ToString(CultureInfo.InvariantCulture))
                  .AppendLine(" more");
            }
        }

        internal static string Describe(VendorOffer offer)
        {
            return $"{offer.MerchantName ?? "(no merchant)"} | item {offer.OutputItemId}"
                 + $" x{offer.OutputCount} | {DescribeCost(offer)}";
        }

        private static string DescribeReprice(OfferChange change)
        {
            return $"{change.After.MerchantName ?? "(no merchant)"} | item {change.After.OutputItemId}"
                 + $" x{change.After.OutputCount} | {DescribeCost(change.Before)}"
                 + $" -> {DescribeCost(change.After)}";
        }

        private static string DescribeRetag(OfferChange change)
        {
            return $"{change.After.MerchantName ?? "(no merchant)"} | item {change.After.OutputItemId}"
                 + $" | festival {change.Before.SeasonalFestival ?? "(none)"}"
                 + $" -> {change.After.SeasonalFestival ?? "(none)"}";
        }

        private static string DescribeCost(VendorOffer offer)
        {
            var parts = new List<string>();

            foreach (var line in (offer.CostLines ?? new List<CostLine>())
                .OrderBy(c => c.Type, StringComparer.Ordinal)
                .ThenBy(c => c.Id))
            {
                parts.Add($"{line.Count}x {line.Type?.ToLowerInvariant() ?? "?"} {line.Id}");
            }

            if (offer.DailyCap.HasValue)
            {
                parts.Add($"daily cap {offer.DailyCap.Value}");
            }

            if (offer.WeeklyCap.HasValue)
            {
                parts.Add($"weekly cap {offer.WeeklyCap.Value}");
            }

            if (offer.SeasonalCap.HasValue)
            {
                parts.Add($"seasonal cap {offer.SeasonalCap.Value}");
            }

            if (offer.HomesteadTier.HasValue)
            {
                parts.Add($"homestead tier {offer.HomesteadTier.Value}");
            }

            return parts.Count == 0 ? "free" : string.Join(", ", parts);
        }

        private static Dictionary<string, VendorOffer> IndexById(IReadOnlyList<VendorOffer> offers)
        {
            var byId = new Dictionary<string, VendorOffer>(StringComparer.Ordinal);
            foreach (var offer in offers)
            {
                if (offer.OfferId != null)
                {
                    byId[offer.OfferId] = offer;
                }
            }

            return byId;
        }

        private static Dictionary<PairKey, List<VendorOffer>> GroupByPair(
            IEnumerable<VendorOffer> offers)
        {
            var grouped = new Dictionary<PairKey, List<VendorOffer>>();
            foreach (var offer in offers)
            {
                var key = new PairKey(offer);
                if (!grouped.TryGetValue(key, out var list))
                {
                    list = new List<VendorOffer>();
                    grouped[key] = list;
                }

                list.Add(offer);
            }

            return grouped;
        }

        // Sorted so the same two datasets always produce the same report,
        // whatever order Dictionary happens to enumerate in.
        private static IEnumerable<PairKey> AllPairs(
            Dictionary<PairKey, List<VendorOffer>> a,
            Dictionary<PairKey, List<VendorOffer>> b)
        {
            return a.Keys.Concat(b.Keys)
                .Distinct()
                .OrderBy(k => k.Merchant, StringComparer.Ordinal)
                .ThenBy(k => k.OutputItemId);
        }
    }
}
