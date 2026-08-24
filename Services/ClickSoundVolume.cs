using System;
using System.Globalization;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Percent (0-100) to MonoGame SoundEffect volume (0.0-1.0) for the
    /// module's UI click. Kept apart from the Blish-coupled player
    /// (Views/Rendering/ClickSound, untestable per repo invariant) so the
    /// mapping and its clamp are covered by real Blish-free tests.
    /// <para>
    /// The clamp is load-bearing, not cosmetic. Measured from the vendored
    /// MonoGame 3.8.0.1641 binary (ilspycmd):
    /// <c>SoundEffectInstance.Volume</c>'s setter THROWS
    /// ArgumentOutOfRangeException outside [0,1] - it does not clamp - and
    /// <c>SoundEffect.Play(volume, pitch, pan)</c> assigns straight through
    /// to it. A persisted percent from a hand-edited settings file must
    /// therefore never reach Play unclamped.
    /// </para>
    /// <para>
    /// The mapping is linear in AMPLITUDE (percent/100 is handed to
    /// SoundEffect.Play as-is; SoundEffect.MasterVolume is MonoGame's
    /// untouched 1.0 default - Blish never assigns it - so the argument is
    /// the whole scale factor). Perceived loudness is not linear in
    /// amplitude, but a linear amplitude slider is what the setting claims
    /// to be: 100 is the asset played at full scale, 0 is silence.
    /// </para>
    /// </summary>
    public static class ClickSoundVolume
    {
        public const int MinPercent = 0;
        public const int MaxPercent = 100;

        /// <summary>
        /// The shipped default, and the ONE line to edit when the
        /// maintainer's field test returns a number.
        /// <para>
        /// Chosen from measurement rather than taste. Today's playback is
        /// <c>ContentService.PlaySoundEffectByName</c>, which plays at
        /// <c>GameService.GameIntegration.Audio.Volume</c> - a value
        /// clamped to [0, 0.4] and, on Blish's default settings, derived
        /// from a rolling average of the GAME's own output peak. So 40
        /// percent reproduces the loudest today can ever be, and 20
        /// percent reproduces Blish's fixed-volume default (its own
        /// "Volume" setting, default 0.2, used when "use game volume" is
        /// off). 75 is 1.875x the absolute ceiling (+5.5 dB) and 3.75x the
        /// fixed default (+11.5 dB), with headroom left above it.
        /// </para>
        /// </summary>
        public const int DefaultPercent = 75;

        public static int Clamp(int percent)
        {
            if (percent < MinPercent) return MinPercent;
            if (percent > MaxPercent) return MaxPercent;
            return percent;
        }

        /// <summary>
        /// True when the setting means "play nothing at all" - the caller
        /// must skip playback entirely rather than play at volume 0, so a
        /// muted click costs no asset load and no pooled voice.
        /// </summary>
        public static bool IsSilent(int percent)
        {
            return Clamp(percent) <= MinPercent;
        }

        /// <summary>
        /// The volume argument for <c>SoundEffect.Play</c>. Always inside
        /// [0,1] - see the type's own comment for why that matters.
        /// </summary>
        public static float ToVolume(int percent)
        {
            return Clamp(percent) / 100f;
        }

        /// <summary>
        /// The slider's live readout, e.g. "75%".
        /// </summary>
        public static string FormatPercent(int percent)
        {
            return Clamp(percent).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// Converts a Blish TrackBar value to a percent. Returns false for
        /// NaN, which is reachable: TrackBar.DoUpdate divides by
        /// <c>Width - 4 - nubWidth</c> while dragging, and its Value setter
        /// clamps with MathHelper.Clamp, whose comparisons both fail for
        /// NaN and let it through unchanged (both measured from the
        /// vendored binaries). A false return means "ignore this value",
        /// never "silence the click".
        /// </summary>
        public static bool TryPercentFromSliderValue(float sliderValue, out int percent)
        {
            percent = MinPercent;

            if (float.IsNaN(sliderValue)) return false;

            // Ordered before the rounding so the infinities land on a bound
            // rather than overflowing the cast.
            if (sliderValue <= MinPercent) return true;
            if (sliderValue >= MaxPercent)
            {
                percent = MaxPercent;
                return true;
            }

            percent = (int)Math.Round(sliderValue, MidpointRounding.AwayFromZero);
            return true;
        }
    }
}
