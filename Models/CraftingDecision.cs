namespace TaimisToolbench.Models
{
    /// <summary>
    /// The display-layer vocabulary for a
    /// <see cref="Models.CraftingTreeNode"/> - what the UI (tree rows, pills)
    /// renders. Deliberately separate from the solver's own
    /// <see cref="Models.AcquisitionSource"/>: it adds <see cref="Have"/>, a
    /// display-only state the solver has no concept of, and never needs that
    /// enum's bookkeeping-only Currency-as-a-decision-source shape. The
    /// single bridge from the solver's vocabulary to this one is
    /// <see cref="Services.CraftingTreeBuilder.MapSource"/>; keep any future
    /// member added here mirrored there. The per-member mapping table, and
    /// why Currency/GuildUpgrade/UnrecognizedIngredient are set directly
    /// rather than mapped, are in docs/ARCHITECTURE.md section S1.5.
    /// <para>
    /// APPEND NEW MEMBERS LAST. This enum has no StringEnumConverter and
    /// <see cref="Models.CraftingTreeNode.Decision"/> round-trips through
    /// PersistedPlan as a raw ordinal int, so inserting a member anywhere
    /// earlier silently reassigns every later member's on-disk integer and
    /// misreads old persisted plans.
    /// </para>
    /// </summary>
    internal enum CraftingDecision
    {
        Craft,
        BuyFromTp,
        BuyFromVendor,
        Have,
        Currency,
        Unknown,
        GuildUpgrade,
        UnrecognizedIngredient,
    }
}
