using System;
using System.Collections.Generic;

namespace VendorOfferUpdater
{
    public class QueryStats
    {
        public int TotalHttpRequests { get; set; }

        public int TotalRowsFetched { get; set; }

        public int DistinctResults { get; set; }

        public int DuplicatesDiscarded { get; set; }

        public int TruncatedPartitions { get; set; }

        public TimeSpan Elapsed { get; set; }

        public List<PartitionStats> Partitions { get; } = new List<PartitionStats>();

        public List<string> NonAlphaVendors { get; } = new List<string>();

        public bool WasInterrupted { get; set; }
    }

    public class PartitionStats
    {
        // Null for the root (unpartitioned) query - only set once a
        // partition has been split by vendor-name prefix.
        public string? Prefix { get; set; }

        public int Depth { get; set; }

        public int RowsAdded { get; set; }

        public int HttpRequests { get; set; }

        public bool WasTruncated { get; set; }
    }

    public class SafetyLimitException : Exception
    {
        public SafetyLimitException(string message)
            : base(message)
        {
        }
    }
}
