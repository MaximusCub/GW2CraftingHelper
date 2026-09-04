using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // The percent -> SoundEffect volume mapping the click player and the
    // Settings tab slider both run through. It lives apart from the
    // Blish-bound player (Views/Rendering/ClickSound) precisely so it can
    // be tested here without referencing UI code (repo invariant).
    //
    // The clamp cases are not defensive decoration: MonoGame 3.8's
    // SoundEffectInstance.Volume setter THROWS ArgumentOutOfRangeException
    // outside [0,1] rather than clamping (measured from the vendored
    // binary), and a percent read from a hand-edited settings.json reaches
    // it unchecked otherwise.
    public class ClickSoundVolumeTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(50, 50)]
        [InlineData(100, 100)]
        [InlineData(-1, 0)]
        [InlineData(-1000, 0)]
        [InlineData(101, 100)]
        [InlineData(int.MaxValue, 100)]
        [InlineData(int.MinValue, 0)]
        public void Clamp_HoldsPercentInsideRange(int percent, int expected)
        {
            Assert.Equal(expected, ClickSoundVolume.Clamp(percent));
        }

        [Theory]
        [InlineData(0, 0f)]
        [InlineData(1, 0.01f)]
        [InlineData(50, 0.5f)]
        [InlineData(75, 0.75f)]
        [InlineData(100, 1f)]
        public void ToVolume_MapsPercentLinearlyOntoZeroToOne(int percent, float expected)
        {
            Assert.Equal((double)expected, (double)ClickSoundVolume.ToVolume(percent), 5);
        }

        // The two endpoints have to be EXACT, not merely close: anything
        // above 1f throws in the Volume setter, and a 0 that is not exactly
        // 0 would still occupy a pooled voice for an inaudible click.
        [Fact]
        public void ToVolume_HitsBothEndpointsExactly()
        {
            Assert.Equal(0f, ClickSoundVolume.ToVolume(ClickSoundVolume.MinPercent));
            Assert.Equal(1f, ClickSoundVolume.ToVolume(ClickSoundVolume.MaxPercent));
        }

        [Theory]
        [InlineData(-5)]
        [InlineData(200)]
        public void ToVolume_ClampsOutOfRangePercentIntoPlayableRange(int percent)
        {
            float volume = ClickSoundVolume.ToVolume(percent);

            Assert.True(volume >= 0f, "volume below 0 would throw in SoundEffectInstance.Volume");
            Assert.True(volume <= 1f, "volume above 1 would throw in SoundEffectInstance.Volume");
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(-3, true)]
        [InlineData(1, false)]
        [InlineData(75, false)]
        [InlineData(100, false)]
        public void IsSilent_IsTrueOnlyAtOrBelowZero(int percent, bool expected)
        {
            Assert.Equal(expected, ClickSoundVolume.IsSilent(percent));
        }

        [Theory]
        [InlineData(0, "0%")]
        [InlineData(7, "7%")]
        [InlineData(75, "75%")]
        [InlineData(100, "100%")]
        [InlineData(-4, "0%")]
        [InlineData(140, "100%")]
        public void FormatPercent_ReadsBackTheClampedPercent(int percent, string expected)
        {
            Assert.Equal(expected, ClickSoundVolume.FormatPercent(percent));
        }

        [Theory]
        [InlineData(0f, 0)]
        [InlineData(50f, 50)]
        [InlineData(100f, 100)]
        [InlineData(49.4f, 49)]
        [InlineData(49.5f, 50)]
        [InlineData(49.6f, 50)]
        [InlineData(-12f, 0)]
        [InlineData(120f, 100)]
        [InlineData(float.PositiveInfinity, 100)]
        [InlineData(float.NegativeInfinity, 0)]
        public void TryPercentFromSliderValue_RoundsAndClampsFiniteValues(float sliderValue, int expected)
        {
            Assert.True(ClickSoundVolume.TryPercentFromSliderValue(sliderValue, out int percent));
            Assert.Equal(expected, percent);
        }

        // TrackBar.DoUpdate divides by (Width - 4 - nubWidth) while
        // dragging and its Value setter's MathHelper.Clamp lets NaN through
        // (both measured), so this is a value the slider handler can really
        // be handed. Rejecting it must not read as "the user asked for 0".
        [Fact]
        public void TryPercentFromSliderValue_RejectsNaNRatherThanSilencingTheClick()
        {
            Assert.False(ClickSoundVolume.TryPercentFromSliderValue(float.NaN, out int percent));
            Assert.Equal(ClickSoundVolume.MinPercent, percent);
        }

        // In-game testing has since returned 35. What must stay true is
        // that the default is a playable percent and that it is clearly
        // louder than what the old path actually played: Blish's
        // fixed-volume default of 0.2 (measured), i.e. 20 percent on this
        // scale. It sits just under that path's absolute 0.4 ceiling,
        // which only a game peaking at full scale ever reached.
        [Fact]
        public void DefaultPercent_IsPlayableAndLouderThanTheOldFixedDefault()
        {
            int percent = ClickSoundVolume.DefaultPercent;

            Assert.Equal(percent, ClickSoundVolume.Clamp(percent));
            Assert.False(ClickSoundVolume.IsSilent(percent));
            Assert.True(percent > 20, "the default must beat the 0.2 fixed volume it replaces");
        }
    }
}
