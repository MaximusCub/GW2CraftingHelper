using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VendorOfferUpdater.Models;

namespace VendorOfferUpdater
{
    public static class VendorOfferHasher
    {
        public static string ComputeOfferId(
            int outputItemId,
            int outputCount,
            IReadOnlyList<CostLine> costLines,
            string merchantName,
            IReadOnlyList<string> locations,
            int? dailyCap,
            int? weeklyCap,
            // M37 (KNOWN-ISSUES #24): optional so every pre-existing call
            // site keeps producing the exact same OfferId it always has -
            // only a caller that explicitly threads a tier value changes
            // the hash. Mirrors Services/VendorOfferHasher.cs exactly.
            int? homesteadTier = null)
        {
            var sb = new StringBuilder();

            sb.Append("output=");
            sb.Append(outputItemId.ToString(CultureInfo.InvariantCulture));
            sb.Append('/');
            sb.Append(outputCount.ToString(CultureInfo.InvariantCulture));

            sb.Append(";costs=");
            var sortedCosts = (costLines ?? Array.Empty<CostLine>())
                .OrderBy(c => c.Type, StringComparer.Ordinal)
                .ThenBy(c => c.Id)
                .ThenBy(c => c.Count)
                .ToList();
            for (int i = 0; i < sortedCosts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(sortedCosts[i].Type);
                sb.Append(':');
                sb.Append(sortedCosts[i].Id.ToString(CultureInfo.InvariantCulture));
                sb.Append(':');
                sb.Append(sortedCosts[i].Count.ToString(CultureInfo.InvariantCulture));
            }

            sb.Append(";merchant=");
            sb.Append(merchantName ?? "");

            sb.Append(";locations=");
            var sortedLocations = (locations ?? Array.Empty<string>())
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < sortedLocations.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(sortedLocations[i]);
            }

            sb.Append(";dailyCap=");
            sb.Append(dailyCap.HasValue
                ? dailyCap.Value.ToString(CultureInfo.InvariantCulture)
                : "null");

            sb.Append(";weeklyCap=");
            sb.Append(weeklyCap.HasValue
                ? weeklyCap.Value.ToString(CultureInfo.InvariantCulture)
                : "null");

            sb.Append(";homesteadTier=");
            sb.Append(homesteadTier.HasValue
                ? homesteadTier.Value.ToString(CultureInfo.InvariantCulture)
                : "null");

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
