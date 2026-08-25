using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
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
            // Optional so a pre-existing call site
            // (none of which know about Homestead tiers) need not pass it,
            // but this does NOT keep the hash byte-for-byte identical to
            // the pre-tier value: the ";homesteadTier=" segment below is
            // appended unconditionally (as "null" when omitted), so any
            // offer's OfferId changes the first time it is recomputed with
            // this code, whether or not its own tier is null.
            int? homesteadTier = null,
            // Astral Acclaim package: same non-backward-
            // compatible-hash caveat as homesteadTier above, appended last
            // so existing positional callers that already pass homesteadTier
            // keep meaning exactly what they meant before this parameter
            // existed. Mirrors tools/VendorOfferUpdater/VendorOfferHasher.cs
            // exactly.
            int? seasonalCap = null)
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
                if (i > 0)
                {
                    sb.Append(',');
                }

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
                if (i > 0)
                {
                    sb.Append(',');
                }

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

            sb.Append(";seasonalCap=");
            sb.Append(seasonalCap.HasValue
                ? seasonalCap.Value.ToString(CultureInfo.InvariantCulture)
                : "null");

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    hex.Append(b.ToString("x2"));
                }

                return hex.ToString();
            }
        }
    }
}
