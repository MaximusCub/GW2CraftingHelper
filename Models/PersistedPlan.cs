using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// On-disk shape for plan persistence across module restarts -
    /// everything Module needs to restore the Crafting Plan tab instantly
    /// on module load, with no network call and no re-solve. See
    /// Services/PlanStore.cs for the store that reads/writes this.
    /// </summary>
    internal class PersistedPlan
    {
        /// <summary>
        /// Bump whenever the persisted graph's SHAPE changes (a member
        /// renamed/removed/retyped anywhere reachable from PersistedPlan)
        /// - PlanStoreHelpers.DeserializePersistedPlan rejects any file
        /// whose SchemaVersion does not match exactly, degrading to the
        /// safe "no restored plan" path instead of a partial render.
        /// PersistedPlanSchemaMemberSetTests reflectively guards the
        /// whole graph against an unbumped shape change.
        /// <para>
        /// <see cref="SchemaVersion"/> deliberately has NO property
        /// initializer: Newtonsoft only overwrites properties present in
        /// the JSON, so an initializer would make a file that omits the
        /// field entirely deserialize as current and sail through the
        /// mismatch check. Construction sites set it explicitly instead.
        /// </para>
        /// </summary>
        public const int CurrentSchemaVersion = 3;

        /// <summary>
        /// SHA-256 of the persisted graph's public member signatures, one
        /// per line, ordinal-sorted - the same list checked in at
        /// tests/shared/persisted_plan_schema.txt.
        /// <para>
        /// It lives here, next to the version it describes, for one reason:
        /// the snapshot test and the version assertion used to be
        /// independent, so adding a property anywhere in the graph could be
        /// made green by editing the test's own expected list alone, with
        /// <see cref="CurrentSchemaVersion"/> left at its old value - which
        /// is precisely the unbumped shape change that makes
        /// PlanStoreHelpers.DeserializePersistedPlan accept a file it can no
        /// longer read correctly. Changing the graph now forces an edit
        /// here, one line from the version.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Last moved by SnapshotItemEntry.Rarity, which is purely ADDITIVE
        /// (see tests/shared/persisted_plan_schema.txt's one-line diff):
        /// Newtonsoft leaves an absent field at its "" initializer, so a
        /// plan or snapshot written before the field existed still
        /// deserializes correctly and <see cref="CurrentSchemaVersion"/>
        /// stays at 3. A bump here would cost every saved plan on disk.
        /// </remarks>
        public const string SchemaShapeHash =
            "76417b936a8017c8f300238dba4d0411da0e2615516e0ecc65b27f0521e09461";

        /// <summary>
        /// See <see cref="CurrentSchemaVersion"/>'s own doc comment for why
        /// this deliberately has NO property initializer.
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// When this plan was originally generated (the same value the
        /// Crafting Plan tab's own "Plan generated - ..." status strip
        /// shows). Reused
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
        /// multi-item batch. On restore this reseeds the input strip's
        /// rows (RestoredRequestInputs.BuildRowSeeds ->
        /// ItemInputRowStrip.RestoreRows), so Generate Plan re-solves the
        /// restored request without any retyping.
        /// </summary>
        public IReadOnlyList<PlanRequestItem> RequestItems { get; set; }

        /// <summary>"Use Own Materials" checkbox state at generation time.</summary>
        public bool UseOwnMaterials { get; set; }

        /// <summary>Price basis (instant-buy vs. buy orders) at generation time.</summary>
        public PriceBasis PriceBasis { get; set; }

        /// <summary>
        /// "Value Own Materials" checkbox state
        /// at generation time - the per-plan session toggle in
        /// Views/CraftingPlanView.cs's controls panel (see its
        /// _valueOwnMaterials field's own doc comment), mirroring <see
        /// cref="UseOwnMaterials"/> above exactly. Added as part of the
        /// <see cref="CurrentSchemaVersion"/> 1 -&gt; 2 bump.
        /// </summary>
        public bool ValueOwnMaterials { get; set; }

        /// <summary>
        /// The full displayed result, including its SolveContext - the
        /// prices/offers/metadata/tree a local
        /// CraftingPlanPipeline.ResolveWithOverrides re-solve needs to keep
        /// working with no network call after a restart.
        /// </summary>
        public CraftingPlanResult Result { get; set; }

        /// <summary>
        /// The user's per-node decision-pill overrides in effect when this
        /// plan was persisted, keyed by solver NodeId. Result already
        /// reflects them, but without persisting the overrides themselves
        /// a restored session's next pill click would re-solve with only
        /// that one override, discarding the rest. Empty (never null) for
        /// a fresh Generate.
        /// </summary>
        public IReadOnlyDictionary<int, AcquisitionSource> NodeOverrides { get; set; }

        /// <summary>
        /// Item ids manually marked "Ignore" when this plan was persisted
        /// - the other half of the override restoration
        /// <see cref="NodeOverrides"/> documents. Empty (never null) for
        /// a fresh Generate.
        /// </summary>
        public IReadOnlyList<int> IgnoredItemIds { get; set; }
    }
}
