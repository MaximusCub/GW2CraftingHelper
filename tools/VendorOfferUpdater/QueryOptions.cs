using System;

namespace VendorOfferUpdater
{
    public class QueryOptions
    {
        public int MaxPrefixDepth { get; init; } = 2;
        public int MaxTotalRequests { get; init; } = 2000;
        public TimeSpan MaxRuntime { get; init; } = TimeSpan.FromMinutes(30);
        public int DelayBetweenRequestsMs { get; init; } = 250;
        public bool DryRun { get; init; }
    }
}
