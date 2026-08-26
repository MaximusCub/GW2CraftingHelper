namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The spacing values that more than one layout class genuinely shares
    /// an ORIGIN with, so a change to one lands at every site that meant it.
    ///
    /// <para>
    /// A number belongs here only when two or more classes want it for the
    /// same reason, and their own doc comments already said so - not when
    /// they merely happen to hold the same integer today. 8px also ships as
    /// LogToolbarLayout.Gap and LogRowLayout.RightPad, and 20px as both
    /// SettingsFormLayout.SectionGap (vertical, between section blocks) and
    /// TreeToolbarRowLayout.GroupGap (horizontal, between button groups);
    /// those are coincidences, they stay where they are, and coupling them
    /// would make a deliberate change to one silently move the others.
    /// </para>
    ///
    /// <para>
    /// Blish-free, in Services, because every consumer is: the *Layout and
    /// *Math classes are the module's testable geometry layer and may not
    /// reference Views.Rendering. Control geometry that DOES need a Blish
    /// type stays in Views/Rendering/UiMetrics.
    /// </para>
    /// </summary>
    internal static class UiSpacing
    {
        /// <summary>
        /// Distance from a panel's left edge to the content inside it.
        /// Claimed independently by three tabs before it was one constant:
        /// Settings' board columns ("the same 16 the section titles sit
        /// at"), the Snapshot header ("left gutter every element on this
        /// tab starts at") and the About tab's facts column.
        /// </summary>
        public const int Inset = 16;

        /// <summary>
        /// Gap between two adjacent buttons that read as one cluster.
        /// SnapshotHeaderLayout named this "the module's one button gap"
        /// while the Settings save bar kept its own copy of the number.
        /// The Recipe Tree toolbar is deliberately NOT a consumer: it packs
        /// its in-group buttons at 4 and states why.
        /// </summary>
        public const int ButtonGap = 8;

        /// <summary>
        /// Gap a section keeps between its right-hand block and the panel's
        /// right edge. NotesSectionLayoutMath already described its own copy
        /// as "shared with every other section"; this is the every-other-
        /// section it meant.
        /// </summary>
        public const int SectionRightPad = 8;
    }
}
