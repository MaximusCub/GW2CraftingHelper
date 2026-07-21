namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Classifies a raw Blish HUD wheel delta (GameService.Input.Mouse.
    /// State.ScrollWheelValue, itself sourced from Blish_HUD.Input.
    /// MouseEventArgs.WheelDelta) as either genuine or corrupted by a real
    /// bug in the vendored library (M36, KNOWN-ISSUES #12 reopened and
    /// root-caused).
    ///
    /// ROOT CAUSE (confirmed by decompiling the shipped BlishHUD v1.3.0
    /// "Blish HUD.exe", Blish_HUD.Input.MouseEventArgs.WheelDelta getter):
    /// <code>
    /// int num = Convert.ToInt32((MouseData &amp; 0xFFFF0000u) &gt;&gt; 16);
    /// if (num &gt; SystemInformation.MouseWheelScrollDelta) num -= 65536;
    /// return num;
    /// </code>
    /// Windows packs a low-level mouse-wheel event's delta as a SIGNED
    /// 16-bit value in the high word of the hook's mouseData field. The
    /// getter above extracts that 16-bit value as UNSIGNED (0..65535) and
    /// tries to recover the sign by subtracting 65536 whenever the unsigned
    /// value exceeds SystemInformation.MouseWheelScrollDelta (120, the
    /// single-notch step). That threshold correctly distinguishes "a single
    /// down-notch" (unsigned 65416, un-wrapped to -120) from "a single
    /// up-notch" (unsigned 120, left alone) - but it silently mis-fires the
    /// moment Windows coalesces 2+ UP-notches into one hook message: an
    /// intended +240 (2 up-notches) reads as unsigned 240, which IS &gt; 120,
    /// so the getter "corrects" a value that was never wrapped in the first
    /// place, turning +240 into 240 - 65536 = -65296. This reproduces
    /// exactly the live-measured histogram (N = coalesced up-notch count,
    /// 2026-07-21 instrumented user trace): N=2 -&gt; -65296, N=3 -&gt; -65176,
    /// N=4 -&gt; -65056, N=5 -&gt; -64936, N=6 -&gt; -64816, N=7 -&gt; -64696,
    /// N=8 -&gt; -64576 (each is N*120 - 65536). A single up-notch (N=1,
    /// unsigned 120) sits exactly AT the threshold (not &gt; 120), so it is -
    /// just barely - left alone, matching the "single notches both
    /// directions are clean" observation. Down-notches never trigger the
    /// mis-fire: their unsigned representation (65536 - N*120) is already
    /// &gt; 120 for every real N, so the subtraction is legitimate and
    /// recovers the correct negative value every time - matching the
    /// observed "fast multi-notch DOWN flicks coalesce CLEANLY" behavior.
    ///
    /// VERDICT (confirmed via GameServices/InputService.cs and both
    /// GameServices/Input/Mouse/{WinApiMouseHookManager,
    /// DebugHelperMouseHookManager}.cs in the vendored source): this bug
    /// lives in MouseEventArgs.WheelDelta itself, which BOTH hook managers
    /// feed identically (WinApiMouseHookManager marshals a real Win32
    /// MSLLHOOKSTRUCT's mouseData directly off the live low-level hook;
    /// DebugHelperMouseHookManager relays the same mouseData field over the
    /// debug-helper IPC channel instead). ApplicationSettings.Instance.
    /// DebugEnabled only picks WHICH hook manager supplies mouseData - it
    /// does not change how WheelDelta interprets it. This is NOT a
    /// DebugHelper-only/dummy-window artifact: a real GW2-attached player
    /// fast-flicking the wheel upward is exposed to the identical
    /// corruption.
    ///
    /// THRESHOLD DERIVATION for this sanitizer: a wrapped-positive event's
    /// raw value is always N*120 - 65536 for some intended up-notch count
    /// N &gt;= 2, i.e. in the band [-65416 (N=1, never actually observed
    /// since N=1 does not mis-fire) .. -60016 (N=46, an already-absurd
    /// flick)], falling further for larger N. A genuine (non-wrapped)
    /// down-delta never gets anywhere near that band: the largest ever
    /// measured is -840 (7 coalesced down-notches), and even a wildly
    /// implausible 40-notch down-flick is only -4800. -60000 sits
    /// comfortably between the two (well below every plausible genuine
    /// delta, well above the entire wrapped band for any realistic notch
    /// count), so "raw &lt;= -60000" identifies exactly the corruption and
    /// nothing else.
    /// </summary>
    public static class WheelDeltaSanitizer
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
    }
}
