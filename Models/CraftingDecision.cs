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
    /// <item><see cref="Currency"/> &lt;- set directly for "Currency"-typed ingredient
    /// nodes only, never via <see cref="Models.AcquisitionSource.Currency"/>. Class-level
    /// follow-up (guildupgrade-ingredients, adversarial review): this branch used to
    /// catch ANY non-"Item"/non-"GuildUpgrade" ingredient node, silently mislabeling an
    /// unrecognized ingredient type as a wallet currency - see
    /// <see cref="Services.CraftingTreeBuilder"/>'s Currency branch, now scoped to the
    /// literal string "Currency".</item>
    /// <item><see cref="Unknown"/> &lt;- <see cref="Models.AcquisitionSource.UnknownSource"/>,
    /// or a missing decision lookup (no solver entry for this node at all) - a genuine
    /// "Item"-typed ingredient with no craftable recipe and no priced source. This is the
    /// ONLY case that legitimately offers the pill layer's interactive IGNORE toggle (see
    /// <see cref="Services.DecisionPillPlanner"/>'s no-options branch) - the user may
    /// genuinely already have the item in hand with no way for the module to know that.</item>
    /// <item><see cref="Have"/> &lt;- no <see cref="Models.AcquisitionSource"/> counterpart;
    /// display-only (owned/zeroed or manually-ignored).</item>
    /// <item><see cref="GuildUpgrade"/> &lt;- set directly for a "GuildUpgrade"-typed
    /// ingredient node (a Guild Decoration recipe's claimed-upgrade requirement, GW2 API
    /// ingredient type), never via <see cref="Models.AcquisitionSource"/>. Deliberately
    /// separate from <see cref="Currency"/>: a guild upgrade id and a wallet currency id
    /// are distinct id spaces with no defined relationship to each other - resolving one
    /// as if it were the other on the strength of a numeric match would risk silently
    /// showing the wrong name or price on any collision - and must
    /// never be priced or named as one - see <see cref="Services.CraftingTreeBuilder"/>'s
    /// "GuildUpgrade" branch and <see cref="Services.PlanSolver"/>'s matching ingredient-loop
    /// branch. Full guild-decoration crafting support (resolving the upgrade's real name,
    /// verifying ownership) is out of scope - see docs/KNOWN-ISSUES.md.</item>
    /// <item><see cref="UnrecognizedIngredient"/> &lt;- set directly for an ingredient node
    /// whose <c>IngredientType</c> is neither "Item", "Currency", nor "GuildUpgrade" (a
    /// future GW2 API ingredient type this module does not yet know how to display), never
    /// via <see cref="Models.AcquisitionSource"/>. Adversarial-review fix
    /// (guildupgrade-ingredients): this used to share <see cref="Unknown"/> with a genuine
    /// no-source "Item" node, which meant <see cref="Services.DecisionPillPlanner"/> could not
    /// tell the two apart and appended the same interactive IGNORE toggle to both - keyed on
    /// <see cref="Models.CraftingTreeNode.ItemId"/>, a raw non-item id here, so toggling it did
    /// nothing for this node yet silently zeroed any genuine "Item" node elsewhere in the tree
    /// that happened to share the same numeric id. Its own decision value routes it to
    /// <see cref="Services.DecisionPillPlanner"/>'s single-locked-pill short-circuit instead,
    /// the same treatment <see cref="Currency"/> and <see cref="GuildUpgrade"/> already get -
    /// see <see cref="Services.CraftingTreeBuilder"/>'s non-"Item" catch-all branch.</item>
    /// </list>
    ///
    /// <see cref="GuildUpgrade"/> and <see cref="UnrecognizedIngredient"/> are appended LAST,
    /// after every pre-existing member, in the order they were introduced: this enum has no
    /// <c>StringEnumConverter</c> (unlike <see cref="Models.AcquisitionSource"/>) and
    /// <see cref="Models.CraftingTreeNode.Decision"/> round-trips through <c>PersistedPlan</c>
    /// as a raw ordinal int - inserting a new member anywhere earlier would silently reassign
    /// every later member's on-disk integer and misread old persisted plans.
    /// </summary>
    public enum CraftingDecision
    {
        Craft,
        BuyFromTp,
        BuyFromVendor,
        Have,
        Currency,
        Unknown,
        GuildUpgrade,
        UnrecognizedIngredient
    }
}
