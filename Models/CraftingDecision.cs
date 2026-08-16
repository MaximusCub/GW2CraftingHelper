namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// The display-layer vocabulary for a <see cref="Models.CraftingTreeNode"/> - what the
    /// UI (tree rows, pills) renders. Deliberately kept separate from the solver's own
    /// <see cref="Models.AcquisitionSource"/> (M38 DO-NOT-TOUCH #15): this enum adds
    /// <see cref="Have"/>, a display-only state the solver has no concept of (an owned or
    /// manually-ignored node - see <see cref="Services.CraftingTreeBuilder"/>'s zero-quantity
    /// and ignored-item-id checks), and it never needs <see cref="Models.AcquisitionSource"/>'s
    /// bookkeeping-only <c>Currency</c>-as-a-decision-source shape (a currency leaf is set to
    /// <see cref="Currency"/> directly, bypassing the solver's decision entirely). The single
    /// bridge from the solver's vocabulary to this one is
    /// <see cref="Services.CraftingTreeBuilder.MapSource"/>; keep any future member added here
    /// mirrored there.
    ///
    /// Per-member mapping from <see cref="Models.AcquisitionSource"/> (see that enum's doc
    /// comment for the full table):
    /// <list type="bullet">
    /// <item><see cref="Craft"/> &lt;- <see cref="Models.AcquisitionSource.Craft"/></item>
    /// <item><see cref="BuyFromTp"/> &lt;- <see cref="Models.AcquisitionSource.BuyFromTp"/></item>
    /// <item><see cref="BuyFromVendor"/> &lt;- <see cref="Models.AcquisitionSource.BuyFromVendor"/></item>
    /// <item><see cref="Currency"/> &lt;- set directly for non-"Item"/non-"GuildUpgrade"
    /// ingredient nodes, never via <see cref="Models.AcquisitionSource.Currency"/>.</item>
    /// <item><see cref="Unknown"/> &lt;- <see cref="Models.AcquisitionSource.UnknownSource"/>,
    /// or a missing decision lookup (no solver entry for this node at all).</item>
    /// <item><see cref="Have"/> &lt;- no <see cref="Models.AcquisitionSource"/> counterpart;
    /// display-only (owned/zeroed or manually-ignored).</item>
    /// <item><see cref="GuildUpgrade"/> &lt;- set directly for a "GuildUpgrade"-typed
    /// ingredient node (a Guild Decoration recipe's claimed-upgrade requirement, GW2 API
    /// ingredient type), never via <see cref="Models.AcquisitionSource"/>. Deliberately
    /// separate from <see cref="Currency"/>: a guild upgrade id is not a wallet currency id
    /// (the two id spaces numerically overlap in real recipe data - see
    /// <see cref="Models.Gw2Constants.KnownCurrencyNames"/>'s own doc comment) and must
    /// never be priced or named as one - see <see cref="Services.CraftingTreeBuilder"/>'s
    /// "GuildUpgrade" branch and <see cref="Services.PlanSolver"/>'s matching ingredient-loop
    /// branch. Full guild-decoration crafting support (resolving the upgrade's real name,
    /// verifying ownership) is out of scope - see docs/KNOWN-ISSUES.md. Appended LAST, after
    /// every pre-existing member: this enum has no <c>StringEnumConverter</c> (unlike
    /// <see cref="Models.AcquisitionSource"/>) and <see cref="Models.CraftingTreeNode.Decision"/>
    /// round-trips through <c>PersistedPlan</c> as a raw ordinal int - inserting a new member
    /// anywhere earlier would silently reassign every later member's on-disk integer and
    /// misread old persisted plans.</item>
    /// </list>
    /// </summary>
    public enum CraftingDecision
    {
        Craft,
        BuyFromTp,
        BuyFromVendor,
        Have,
        Currency,
        Unknown,
        GuildUpgrade
    }
}
