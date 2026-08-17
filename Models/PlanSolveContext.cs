using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Everything needed to re-solve a generated plan locally (no network):
    /// the reduced tree plus the fetched prices, offers, and metadata from
    /// the originating generation. Enables instant per-node override
    /// recomputes in the UI.
    /// </summary>
    public class PlanSolveContext
    {
        public int TargetItemId { get; set; }
        public int Quantity { get; set; }
        public RecipeNode Tree { get; set; }
        public IReadOnlyDictionary<int, ItemPrice> Prices { get; set; }
        public IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> VendorOffers { get; set; }
        public IReadOnlyDictionary<int, ItemMetadata> Metadata { get; set; }
        public ISet<int> LearnedRecipeIds { get; set; }
        public List<UsedMaterial> UsedMaterials { get; set; }
        public PriceBasis PriceBasis { get; set; }

        /// <summary>
        /// Currency name/icon metadata snapshotted at GENERATION time, so
        /// that ResolveWithOverrides' local re-solve can reuse it on
        /// CurrencyCost rows without any network call (same reasoning as
        /// Prices/VendorOffers/Metadata above).
        /// </summary>
        public IReadOnlyDictionary<int, CurrencyMetadata> CurrencyMetadata { get; set; }

        /// <summary>
        /// The currency valuation in effect at GENERATION time, snapshotted
        /// here alongside Prices/VendorOffers/Metadata. This is intentional:
        /// ResolveWithOverrides re-solves locally and, like prices and
        /// vendor data, deliberately reuses the generation-time valuation
        /// rather than re-reading live settings - a local override toggle
        /// must not silently re-price the plan out from under the user with
        /// whatever the settings say right now. Freshly edited rates apply
        /// starting with the next full Generate.
        /// </summary>
        public CurrencyValuation CurrencyValuation { get; set; }

        /// <summary>
        /// The own-materials valuation mode in effect at GENERATION time,
        /// snapshotted here for the same reason as CurrencyValuation: a
        /// local override re-solve must keep pricing owned materials the
        /// way the original Generate did, not whatever CraftingPlanView's
        /// per-plan "Value Own Materials" checkbox currently shows (VOM
        /// design Section 5.2 - this is a per-plan session choice, not a
        /// live-read global setting, exactly like PriceBasis above). A
        /// freshly toggled checkbox applies starting with the next full
        /// Generate. Valued now covers both the pre-existing 15% sell-back
        /// force-buy guard AND the decision-invariant reduction guide fed
        /// into InventoryReducer.Reduce (see that method's zeroOwnedDecisions
        /// doc comment) - Free behaves exactly as before this design.
        /// </summary>
        public OwnMaterialsMode OwnMaterialsMode { get; set; }

        /// <summary>
        /// Wiki-derived acquisition hints snapshotted at GENERATION time, so
        /// that ResolveWithOverrides' local re-solve can keep hint text on
        /// unpriceable nodes without any refetch (same reasoning as
        /// CurrencyMetadata above - this is a static local seed, not a live
        /// fetch, but the snapshot keeps the two code paths symmetric).
        /// </summary>
        public IReadOnlyDictionary<int, AcquisitionHint> AcquisitionHints { get; set; }

        /// <summary>
        /// Wiki-verified daily-craft-cooldown data snapshotted at GENERATION
        /// time, mirroring AcquisitionHints immediately above (same
        /// static-local-seed reasoning) - so ResolveWithOverrides' local
        /// re-solve keeps producing craft-cooldown notices without any
        /// refetch.
        /// </summary>
        public IReadOnlyDictionary<int, DailyCooldownItem> DailyCooldownItems { get; set; }

        /// <summary>
        /// Per-node owned-quantity attribution snapshotted at GENERATION
        /// time (M34-B2a #1, see ReducedTreeResult.OwnedQuantityUsedByNode
        /// and CraftingPlanPipeline.BuildOwnedQuantityUsedByNodeId) - NodeId
        /// is stable across repeat Solve() calls on the same Tree object, so
        /// ResolveWithOverrides' local re-solve reuses this as-is rather
        /// than recomputing it (reduction itself never re-runs locally -
        /// see Tree's own doc comment).
        /// </summary>
        public IReadOnlyDictionary<int, int> OwnedQuantityUsedByNodeId { get; set; }

        /// <summary>
        /// Owned amount per currency id referenced by the plan's
        /// CurrencyCosts, snapshotted at GENERATION time (M34-B2a #4 - see
        /// AccountCurrencyIndex). Cosmetic display data only; null when no
        /// wallet snapshot was available or the plan needed no currency.
        /// </summary>
        public IReadOnlyDictionary<int, int> OwnedCurrencyAmounts { get; set; }

        /// <summary>
        /// W4B (vendor cost-component leaves): owned amount per item id
        /// that appears as a TP-valued Item cost line on any winning
        /// BuyFromVendor decision in the plan, snapshotted at GENERATION
        /// time the same way OwnedCurrencyAmounts is (see
        /// CraftingPlanPipeline.BuildOwnedVendorItemComponentAmounts) -
        /// cosmetic display data only, feeding ONLY a component leaf's
        /// informational HAVE pill (CraftingTreeNode.ComponentOwnedQuantity)
        /// - never consulted by InventoryReducer or PlanSolver, so it can
        /// never affect a decision, a total, or Quantity itself. Null under
        /// the same conditions as OwnedCurrencyAmounts (no wallet/inventory
        /// snapshot, or no vendor Item cost component anywhere in the plan).
        /// </summary>
        public IReadOnlyDictionary<int, int> OwnedVendorItemAmounts { get; set; }

        /// <summary>
        /// NodeIds gw2e's "Value Own Materials" force-buy pre-pass excluded
        /// from crafting at GENERATION time (M34-B2a #3 - see
        /// OwnedMaterialsForceBuyPrePass), snapshotted here so
        /// ResolveWithOverrides' local re-solve keeps applying it to every
        /// node the user hasn't manually overridden, rather than forgetting
        /// it the moment any single pill is clicked. Null in
        /// OwnMaterialsMode.Free (the pre-pass never ran).
        /// </summary>
        public ISet<int> ForceBuyOnlyNodeIds { get; set; }

        /// <summary>
        /// Verification-review fix: the narrower, competency-independent
        /// subset of ForceBuyOnlyNodeIds above, snapshotted at GENERATION
        /// time alongside it - see OwnedMaterialsForceBuyPrePass.
        /// ForceBuyPrePassResult's own doc comment for what distinguishes
        /// the two sets. ResolveWithOverrides' local re-solve reapplies
        /// this to PlanSolver.Solve's competencyIndependentForceBuyNodeIds
        /// parameter on every re-solve, the same way it already reapplies
        /// ForceBuyOnlyNodeIds - without it, a local override re-solve
        /// would silently fall back to "never suppress
        /// CheapestCraftUntrained" (null default), diverging from the
        /// original generation's own Plan Notes. Null under the exact same
        /// conditions as ForceBuyOnlyNodeIds (the pre-pass never ran).
        /// </summary>
        public ISet<int> CompetencyIndependentForceBuyNodeIds { get; set; }

        /// <summary>
        /// M35 (gw2efficiency parity - multi-item plans): the original
        /// per-item request snapshotted at GENERATION time, for the same
        /// reason as CurrencyValuation/OwnMaterialsMode above - so a local
        /// override re-solve (ResolveWithOverrides) can keep populating
        /// CraftingPlanResult.RequestedItems/MultiItemRoots consistently on
        /// every re-solve of a multi-item batch, not just the first
        /// generation. Null for a single-item plan (Tree is then the real
        /// item's own tree, not the synthetic wrapper).
        /// </summary>
        public IReadOnlyList<PlanRequestItem> RequestedItems { get; set; }

        /// <summary>
        /// The Homestead Refinement efficiency tier configuration in effect
        /// at GENERATION time (M37, KNOWN-ISSUES #24), snapshotted here for
        /// the same reason as CurrencyValuation/OwnMaterialsMode above: a
        /// local override re-solve (ResolveWithOverrides) must keep gating
        /// Homestead offers the way the original Generate did, not whatever
        /// the setting reads right now. A freshly changed tier setting
        /// applies starting with the next full Generate.
        /// </summary>
        public HomesteadEfficiencyTiers HomesteadTiers { get; set; }

        /// <summary>
        /// Per-character crafting discipline data snapshotted at GENERATION
        /// time (W3C - see AccountSnapshot.CharacterDisciplines), for the
        /// same reason as OwnedCurrencyAmounts above: so a local override
        /// re-solve (ResolveWithOverrides) can keep populating
        /// CraftingPlanResult.CharacterDisciplines on every re-solve, not
        /// just the first generation. Cosmetic display data only; null
        /// under the same conditions as the source field.
        /// </summary>
        public IReadOnlyList<SnapshotCharacterDiscipline> CharacterDisciplines { get; set; }

        /// <summary>
        /// VOM finding #1 fix: the ORIGINAL, unreduced tree from GENERATION
        /// time (the same `tree` OwnedMaterialsForceBuyPrePass and the
        /// zero-owned decision pass ran against - see CraftingPlanPipeline's
        /// Step 5.5/5.6), snapshotted here ONLY when the force-buy pre-pass
        /// ran (ForceBuyOnlyNodeIds != null) so ResolveWithOverrides can
        /// re-run InventoryReducer.Reduce with an overrides-aware guide on
        /// every local re-solve, instead of permanently replaying overrides
        /// against the ALREADY-reduced Tree above - which froze ingredient
        /// discounts against the zero-owned decision forever, even after a
        /// manual override flips a force-buy-flagged node to Craft. Null
        /// whenever the pre-pass did not run (Free mode, or no snapshot/
        /// reducer at generation time) - Tree/UsedMaterials above are
        /// already final and correct in that case, so no re-reduction is
        /// needed or possible. See AccountItems/ActiveCharacterName below,
        /// both populated under the exact same condition as this field.
        /// </summary>
        public RecipeNode UnreducedTree { get; set; }

        /// <summary>
        /// The raw owned-item entries InventoryReducer.Reduce's
        /// AccountItemIndex was built from at GENERATION time (NOT the
        /// AccountItemIndex itself - that type lives in the Services
        /// namespace, and Models deliberately never references Services),
        /// retained so ResolveWithOverrides can rebuild an identical index
        /// and re-run reduction against the SAME owned-material pool - see
        /// UnreducedTree's doc comment for when this is populated.
        /// </summary>
        public IReadOnlyList<SnapshotItemEntry> AccountItems { get; set; }

        /// <summary>
        /// The active character name at GENERATION time, threaded through
        /// to a re-reduction the same way UnreducedTree/AccountItems are -
        /// see UnreducedTree's doc comment.
        /// </summary>
        public string ActiveCharacterName { get; set; }
    }
}
