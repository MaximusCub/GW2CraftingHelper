using System;
using System.Globalization;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Percent (0-100) to MonoGame SoundEffect volume (0.0-1.0) for the
    /// module's UI click, kept apart from the Blish-coupled player
    /// (Views/Rendering/ClickSound) so it is covered by Blish-free tests.
    /// </summary>
    public static class ClickSoundVolume
    {
        public const int MinPercent = 0;
        public const int MaxPercent = 100;

        // The maintainer's field-tested number: 0.875x the 0.4 ceiling
        // Blish's game-derived click volume can ever reach (-1.2 dB) and
        // 1.75x its 0.2 fixed-volume default (+4.9 dB), which puts the
        // asset's own 0.357 peak at -18.1 dBFS. Louder than Blish ever
        // played it, quiet enough to click all day.
        public const int DefaultPercent = 35;

        // Load-bearing: SoundEffectInstance.Volume's setter throws outside
        // [0,1] rather than clamping, and SoundEffect.Play assigns straight
        // through to it (measured, MonoGame 3.8.0.1641) - a hand-edited
        // settings file must never reach Play unclamped.
        public static int Clamp(int percent)
        {
            if (percent < MinPercent) return MinPercent;
            if (percent > MaxPercent) return MaxPercent;
            return percent;
        }

        // True when the caller should skip playback entirely, so a muted
        // click costs no asset load and no pooled voice.
        public static bool IsSilent(int percent)
        {
            return Clamp(percent) <= MinPercent;
        }

        // Linear in amplitude, deliberately - not in perceived loudness.
        public static float ToVolume(int percent)
        {
            return Clamp(percent) / 100f;
        }

        public static string FormatPercent(int percent)
        {
            return Clamp(percent).ToString(CultureInfo.InvariantCulture) + "%";
        }

        // Converts a Blish TrackBar value to a percent. NaN is reachable
        // (TrackBar divides by a width term while dragging, and
        // MathHelper.Clamp passes NaN through); a false return means
        // "ignore this value", never "silence the click".
        public static bool TryPercentFromSliderValue(float sliderValue, out int percent)
        {
            percent = MinPercent;

            if (float.IsNaN(sliderValue)) return false;

            // Bound the infinities before the rounding cast can overflow.
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
