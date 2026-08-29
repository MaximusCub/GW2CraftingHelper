namespace TaimisToolbench.Services
{
    /// <summary>
    /// Classifies a raw Blish HUD wheel delta (GameService.Input.Mouse.
    /// State.ScrollWheelValue) as genuine or corrupted by a defect in the
    /// shipped Blish HUD v1.3.0 binary: it extracts Windows' signed 16-bit
    /// wheel delta as UNSIGNED, then "un-wraps" anything above one notch,
    /// so an intended +240 (two coalesced up-notches) arrives as
    /// 240 - 65536 = -65296 (KNOWN-ISSUES #12).
    ///
    /// The fact a caller needs: raw &lt;= -60000 is always a wrapped
    /// positive delta and never a genuine down-scroll, and adding 65536
    /// back recovers the intended value. The decompiled getter, the
    /// live-measured histogram behind that threshold, and why 120 is
    /// hardcoded here are in docs/ARCHITECTURE.md section 2.
    /// </summary>
    internal static class WheelDeltaSanitizer
    {
        /// <summary>
        /// See the class doc comment's threshold derivation. Any raw delta
        /// at or below this value is the wrapped-positive corruption,
        /// never a genuine down-scroll.
        /// </summary>
        private const int WrapThreshold = -60000;

        /// <summary>
        /// The exact amount MouseEventArgs.WheelDelta over-subtracts for a
        /// mis-classified positive delta (ushort.MaxValue + 1) - adding it
        /// back recovers the original, never-actually-negative value.
        /// </summary>
        private const int WrapCorrection = 65536;

        /// <summary>
        /// Classifies a raw wheel delta. Zero-allocation (a plain value
        /// tuple of two primitives) since this runs unconditionally on the
        /// wheel path, not gated on diagnostics.
        /// </summary>
        /// <param name="rawDelta">
        /// The raw value read from GameService.Input.Mouse.State.
        /// ScrollWheelValue for the current wheel event.
        /// </param>
        /// <returns>
        /// IsWrapped is true when <paramref name="rawDelta"/> is the
        /// wrapped-positive corruption described in the class doc comment;
        /// IntendedDelta is the corrected value (always positive when
        /// IsWrapped) in that case, or <paramref name="rawDelta"/>
        /// unchanged otherwise.
        /// </returns>
        public static (bool IsWrapped, int IntendedDelta) Classify(int rawDelta)
        {
            if (rawDelta <= WrapThreshold)
            {
                return (true, rawDelta + WrapCorrection);
            }

            return (false, rawDelta);
        }

        /// <summary>
        /// Windows' "one screen at a time" mouse-wheel-lines setting reports
        /// SystemInformation.MouseWheelScrollLines as -1, not a usable line
        /// count. Used directly, that sign flips
        /// CraftingPlanView.ApplyWheelWrapCorrection's corrective pixel delta
        /// for every wrapped up-flick. This substitutes Windows' documented
        /// out-of-box default of 3 lines whenever the raw value is not a
        /// usable positive count. It does not reproduce Blish's own step size
        /// under that setting - Blish's step is itself wrong there, and this
        /// sanitizer cannot fix Blish's arithmetic. See
        /// docs/ARCHITECTURE.md section 2.
        /// </summary>
        /// <param name="rawLines">
        /// The raw value read from
        /// System.Windows.Forms.SystemInformation.MouseWheelScrollLines.
        /// </param>
        /// <returns>
        /// <paramref name="rawLines"/> unchanged when positive; otherwise
        /// 3 (Windows' documented default line count).
        /// </returns>
        public static int SanitizeScrollLines(int rawLines)
        {
            return rawLines > 0 ? rawLines : 3;
        }
    }
}
