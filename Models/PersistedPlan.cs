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
        /// forbids.
        /// <para>
        /// Round 2 review-fix (mustFix): <see cref="SchemaVersion"/> has NO
        /// property initializer (see that property) - the CLR default for
        /// an unset int is 0, distinct from CurrentSchemaVersion (1) above.
        /// This is deliberate, not an oversight: Newtonsoft.Json only
        /// overwrites properties that are actually PRESENT in the source
        /// JSON, so a `= CurrentSchemaVersion` initializer here would run
        /// in the object's default constructor and then survive untouched
        /// for any file whose JSON omits "SchemaVersion" entirely (every
        /// file written before this field existed) - deserializing it as
        /// CurrentSchemaVersion, sailing straight through the mismatch
        /// check below, and rendering whatever members that older schema
        /// happened to be missing as silently null. Both real construction
        /// sites (Module.cs's PersistAfterGenerateAsync/
        /// PersistResolvedPlanInBackground) set SchemaVersion =
        /// CurrentSchemaVersion explicitly instead, so every file this
        /// module itself ever writes still carries the current value; only
        /// a file this code never wrote (missing the field, or carrying an
        /// explicit old value) deserializes as anything else.
        /// </para>
        /// <para>
        /// VOM design (Section 5.4): bumped 1 -&gt; 2 for the new <see
        /// cref="ValueOwnMaterials"/> field below - the first real exercise
        /// of this reject-and-regenerate mechanism since it was introduced.
        /// A SchemaVersion-1 file is now rejected (not silently defaulted
        /// to <c>false</c>) by PlanStoreHelpers.DeserializePersistedPlan,
        /// degrading to Module's existing "no restored plan" path (one Warn
        /// log line, empty Crafting Plan tab on first load after upgrade) -
        /// a known, already-exercised, safe fresh-start, not a crash.
        /// </para>
        /// <para>
        /// Quality-audit fix (B1): bumped 2 -&gt; 3 because the graph this
        /// version number is supposed to cover grew ~275 lines of new
        /// members (CraftingTreeNode's CraftCostBreakdown/
        /// BuyFromTpCostBreakdown/BuyFromVendorCostBreakdown among others,
        /// PlanSolveContext's CompetencyIndependentForceBuyNodeIds/
        /// UnreducedTree/AccountItems/ActiveCharacterName,
        /// CraftingPlanResult's ExcessCraftOutputs/
        /// RecipeSheetSavingsOpportunities/SeasonalVendorTips among
        /// others) after the 1 -&gt; 2 bump without a matching version bump -
        /// exactly the silent-default failure this constant's doc comment
        /// says it exists to reject: a SchemaVersion-2 file written by an
        /// older build would have restored with all of those fields
        /// silently null instead of being rejected. See
        /// tests/Models/PersistedPlanSchemaMemberSetTests.cs for the guard
        /// that now catches a repeat of this - it fails, independent of
        /// SchemaVersion, whenever the public member set of PersistedPlan,
        /// CraftingPlanResult, PlanSolveContext, or CraftingTreeNode
        /// changes, which is the prompt to bump this constant again.
        /// </para>
        /// </summary>
        public const int CurrentSchemaVersion = 3;

        /// <summary>
        /// See <see cref="CurrentSchemaVersion"/>'s own doc comment for why
        /// this deliberately has NO property initializer.
        /// </summary>
        public int SchemaVersion { get; set; }

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
        /// VOM design (Section 5.3): "Value Own Materials" checkbox state
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
