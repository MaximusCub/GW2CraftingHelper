using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VendorOfferUpdater.Models;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Computes the SHA-256 <c>offerId</c> that keys every row in
    /// ref/vendor_offers.json. Sole implementation: the module used to
    /// carry a hand-maintained copy in Services/, which nothing in the
    /// module ever called, and which was deleted rather than kept in sync.
    /// <para>
    /// What pins the output is tests/shared/vendor_offer_hasher_vectors.json,
    /// replayed by VendorOfferHasherGoldenVectorTests in this project's
    /// suite. Any change to the string built below - segment order, names,
    /// separators, sort rules, the "null" spelling - changes every id in
    /// the 15MB dataset, so a deliberate format change means regenerating
    /// that fixture AND accepting that existing rows keep their old ids
    /// only for as long as --merge-into copies untouched baseline objects
    /// through verbatim.
    /// </para>
    /// </summary>
    public static class VendorOfferHasher
    {
        public static string ComputeOfferId(
            int outputItemId,
            int outputCount,
            IReadOnlyList<CostLine>? costLines,
            string? merchantName,
            IReadOnlyList<string>? locations,
            int? dailyCap,
            int? weeklyCap,
            // Optional so a caller need not pass
            // it, but this does NOT keep the hash byte-for-byte identical
            // to the pre-tier value: the ";homesteadTier=" segment below is
            // appended unconditionally (as "null" when omitted), so any
            // offer's OfferId changes the first time it is recomputed with
            // this code, whether or not its own tier is null. Existing
            // rows only stay stable because callers like --merge-into copy
            // untouched baseline objects through rather than recomputing
            // them.
            int? homesteadTier = null,
            // Astral Acclaim package: same non-backward-
            // compatible-hash caveat as homesteadTier above, appended last
            // so existing positional callers that already pass homesteadTier
            // keep meaning exactly what they meant before this parameter
            // existed.
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

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
