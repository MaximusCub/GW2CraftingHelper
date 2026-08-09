using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// On-disk shape for W3D (plan persistence across module restarts) -
    /// everything Module needs to restore the Crafting Plan tab instantly
    /// on module load, with no network call and no re-solve. See
    /// Services/PlanStore.cs for the store that reads/writes this.
    /// </summary>
    public class PersistedPlan
    {
        /// <summary>
        /// Review-fix (W3D adversarial review, mustFix): bumped only when
        /// this schema's SHAPE changes (a member renamed/removed/retyped on
        /// this class, CraftingPlanResult, or PlanSolveContext in a way
        /// that would leave old data silently defaulted instead of
        /// rejected). PlanStoreHelpers.DeserializePersistedPlan rejects any
        /// file whose SchemaVersion does not match exactly - the only
        /// "old-schema file" detection PersistedPlan had before this fix
        /// was the purely structural `Result?.Plan != null` check, which a
        /// future rename could still pass while every renamed/removed
        /// member came back silently null - a "partial render" spec item 4
        /// forbids. A file from before this field existed deserializes
        /// SchemaVersion as 0 (Newtonsoft's default for a missing int
        /// property), which never equals CurrentSchemaVersion, so it is
        /// correctly treated as old-schema too.
        /// </summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>See <see cref="CurrentSchemaVersion"/>.</summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// When this plan was originally generated (the same value the
        /// Crafting Plan tab's own "Generated: ..." header shows). Reused
        /// as-is by a later local override re-solve (see
        /// CraftingPlanPipeline.ResolveWithOverrides) - an override click
        /// re-solves locally with the SAME prices, it does not re-generate,
        /// so this timestamp (and the staleness banner it drives on
        /// restore) must not silently advance just because the user
        /// clicked a decision pill.
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// The original request (item ids + quantities) this plan was
        /// generated for, in request order - mirrors
        /// CraftingPlanResult.RequestedItems' own shape, but populated for
        /// every plan (single- or multi-item), not only a genuine
        /// multi-item batch. Not used to reconstruct the search box/
        /// quantity inputs on restore (W3D spec item 5 - those stay at
        /// their defaults); persisted for round-trip fidelity of the
        /// original request only.
        /// </summary>
        public IReadOnlyList<PlanRequestItem> RequestItems { get; set; }

        /// <summary>"Use Own Materials" checkbox state at generation time.</summary>
        public bool UseOwnMaterials { get; set; }

        /// <summary>Price basis (instant-buy vs. buy orders) at generation time.</summary>
        public PriceBasis PriceBasis { get; set; }

        /// <summary>
        /// The full displayed result, including its SolveContext - the
        /// prices/offers/metadata/tree a local
        /// CraftingPlanPipeline.ResolveWithOverrides re-solve needs to keep
        /// working with no network call after a restart (W3D spec item 3's
        /// correctness bar).
        /// </summary>
        public CraftingPlanResult Result { get; set; }

        /// <summary>
        /// Review-fix (W3D adversarial review, critical): the user's
        /// per-node decision-pill overrides (Craft/Buy TP/Buy Vendor) in
        /// effect when this plan was last persisted, keyed by the same
        /// solver NodeId TreeSectionController's own _nodeOverrides
        /// dictionary uses. <see cref="Result"/> already reflects these
        /// overrides (it is the OUTPUT of applying them), but without
        /// persisting the overrides themselves too, a restored session's
        /// override loop starts from empty - the very next pill click would
        /// re-solve with only that ONE new override applied, silently
        /// discarding every override the user set before restarting. Empty
        /// (never null) for a plan persisted straight after a fresh
        /// Generate, which has no overrides yet.
        /// </summary>
        public IReadOnlyDictionary<int, AcquisitionSource> NodeOverrides { get; set; }

        /// <summary>
        /// Review-fix (W3D adversarial review, critical): item ids manually
        /// marked "Ignore" (see TreeSectionController's own _ignoredItemIds
        /// doc comment) in effect when this plan was last persisted - the
        /// other half of the override-restoration fix <see
        /// cref="NodeOverrides"/> documents. Empty (never null) for a plan
        /// persisted straight after a fresh Generate.
        /// </summary>
        public IReadOnlyList<int> IgnoredItemIds { get; set; }
    }
}
