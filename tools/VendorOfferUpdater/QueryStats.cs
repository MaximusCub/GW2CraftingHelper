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
        public List<PartitionStats> Partitions { get; } = new();
        public List<string> NonAlphaVendors { get; } = new();
    }

    public class PartitionStats
    {
        public string Prefix { get; set; }
        public int Depth { get; set; }
        public int RowsAdded { get; set; }
        public int HttpRequests { get; set; }
        public bool WasTruncated { get; set; }
    }

    public class SafetyLimitException : Exception
    {
        public SafetyLimitException(string message) : base(message) { }
    }
}
