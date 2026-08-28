using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// One Plan History index row - see Services/PlanHistoryStore.cs for
    /// the store and Services/PlanHistoryBlobStore.cs for the per-entry
    /// PersistedPlan blob this row links to by EntryId.
    /// <para>
    /// Deliberately carries NO type reachable from PersistedPlan's result
    /// graph (no CraftingPlanResult, no PlanSolveContext, no tree), so it
    /// sits outside both PersistedPlanSchemaMemberSetTests' reflective
    /// guard and PlanStoreHelpers' exact-version rejection. A
    /// PersistedPlan.CurrentSchemaVersion bump therefore discards only
    /// blobs: every row survives, the list still renders, and a row
    /// degrades from "Open" to "Re-solve" with a visible reason.
    /// </para>
    /// </summary>
    internal class PlanHistoryEntry
    {
        /// <summary>Guid.NewGuid().ToString("N"); also the blob filename.</summary>
        public string EntryId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime LastGeneratedAtUtc { get; set; }

        public bool Pinned { get; set; }

        // Request identity - exactly Module's own PersistedPlanMetadata
        // four fields plus the one item-id-keyed set that is safely
        // comparable across solves. Homestead tiers and currency valuation
        // are deliberately absent: the module already treats these fields
        // as "the request identity", and adding more here would fork that
        // definition. Read anything else off the blob.
        public IReadOnlyList<PlanRequestItem> RequestItems { get; set; }

        public bool UseOwnMaterials { get; set; }

        public PriceBasis PriceBasis { get; set; }

        public bool ValueOwnMaterials { get; set; }

        public IReadOnlyList<int> IgnoredItemIds { get; set; }

        // Denormalized display summary: the list renders with no blob read
        // and no metadata fetch - the same duplication pattern
        // SnapshotItemEntry and RankerWatchlistEntry already use.
        public IReadOnlyList<PlanHistoryItemSummary> ItemSummaries { get; set; }

        public long TotalCoinCostAtGeneration { get; set; }

        public int OverrideCountAtGeneration { get; set; }

        public int IgnoredCountAtGeneration { get; set; }

        // Blob linkage.
        public bool BlobPresent { get; set; }

        /// <summary>The PersistedPlan.SchemaVersion the blob was written at.</summary>
        public int BlobSchemaVersion { get; set; }

        /// <summary>Capped at PlanHistoryRetention.MaxCostSamples, oldest dropped.</summary>
        public IReadOnlyList<PlanHistorySample> CostSamples { get; set; }
    }

    /// <summary>One requested item's display summary on an index row.</summary>
    internal class PlanHistoryItemSummary
    {
        /// <summary>Internal-only, never rendered.</summary>
        public int ItemId { get; set; }

        public string Name { get; set; }

        public string IconUrl { get; set; }

        /// <summary>GW2 API rarity string, for IconControls' frame colour.</summary>
        public string Rarity { get; set; }

        public int Quantity { get; set; }
    }

    /// <summary>One cost-over-time sample, appended on each dedup bump.</summary>
    internal class PlanHistorySample
    {
        public DateTime TimestampUtc { get; set; }

        public long TotalCoinCost { get; set; }
    }

    /// <summary>The whole data/plan_history.json file.</summary>
    internal class PlanHistoryIndex
    {
        public const int CurrentSchemaVersion = 1;

        // NO property initializer - same reason as PersistedPlan.
        // SchemaVersion: Newtonsoft only overwrites members present in the
        // JSON, so an initializer would make a file that omits the field
        // deserialize as current and sail through the mismatch check.
        // Construction sites set it explicitly instead.
        public int SchemaVersion { get; set; }

        public List<PlanHistoryEntry> Entries { get; set; } = new List<PlanHistoryEntry>();
    }
}
