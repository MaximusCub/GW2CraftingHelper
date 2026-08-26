using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Five layout constants used to share a name across same-namespace
    /// static classes while holding different pixel values - ButtonGap 8 vs
    /// 4, RowHeight 30 vs 35, RowGap 10 vs 3, NameRunChars 22 vs 45, Inset
    /// 16 in three classes - so reaching for the wrong class name compiled
    /// cleanly and shipped a misalignment. The rename that separated them,
    /// and the move of the three genuinely-shared values to
    /// <see cref="UiSpacing"/>, were both required to be numerically
    /// neutral. These are the literals each constant held beforehand: a
    /// later edit that changes one of these numbers is a deliberate visual
    /// change and has to be made here too.
    /// </summary>
    public class LayoutConstantCollisionTests
    {
        [Fact]
        public void RenamedConstants_KeepTheirPreRenameValues()
        {
            Assert.Equal(8, SettingsSaveBarLayout.SettingsSaveBarButtonGap);
            Assert.Equal(4, TreeToolbarRowLayout.TreeToolbarButtonGap);
            Assert.Equal(30, SettingsFormLayout.SettingsRowHeight);
            Assert.Equal(35, TopRegionLayoutMath.TopRegionRowHeight);
            Assert.Equal(10, SettingsFormLayout.SettingsRowGap);
            Assert.Equal(3, TopRegionLayoutMath.TopRegionRowGap);
            Assert.Equal(22, SettingsFormLayout.SettingsNameRunChars);
            Assert.Equal(22, SettingsCurrencyGridLayout.SettingsCurrencyNameRunChars);
            Assert.Equal(45, SnapshotItemGridLayout.SnapshotNameRunChars);
            Assert.Equal(16, AboutLayoutMath.AboutInset);
            Assert.Equal(16, SnapshotHeaderLayout.SnapshotHeaderInset);
            Assert.Equal(16, SettingsSaveBarLayout.SettingsSaveBarInset);
        }

        [Fact]
        public void ConstantsMovedOntoUiSpacing_KeepTheirPreMoveValues()
        {
            Assert.Equal(16, SettingsFormLayout.CellLeftPad);
            Assert.Equal(8, SnapshotHeaderLayout.HeaderButtonGap);
            Assert.Equal(8, PlanRelayoutMath.TableRightMargin);
            Assert.Equal(8, NotesSectionLayoutMath.RightPadding);
        }

        /// <summary>
        /// The differences the rename exists to protect. Each pair reads the
        /// same way in prose and must not converge: a later "consistency"
        /// edit that unifies one of them is a visual change to a tab, not a
        /// tidy-up, and fails here first.
        /// </summary>
        [Fact]
        public void DeliberatelyDifferentValues_StayDifferent()
        {
            // The Recipe Tree toolbar packs its in-group buttons tighter
            // than the module's button gap on purpose - see its own comment
            // on GroupGap.
            Assert.NotEqual(UiSpacing.ButtonGap, TreeToolbarRowLayout.TreeToolbarButtonGap);
            Assert.NotEqual(SettingsFormLayout.SettingsRowHeight, TopRegionLayoutMath.TopRegionRowHeight);
            Assert.NotEqual(SettingsFormLayout.SettingsRowGap, TopRegionLayoutMath.TopRegionRowGap);
            Assert.NotEqual(
                SettingsFormLayout.SettingsNameRunChars, SnapshotItemGridLayout.SnapshotNameRunChars);
        }
    }
}
