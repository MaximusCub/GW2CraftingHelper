namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure scroll-restoration arithmetic (Blish-free, unit-testable).
    /// Blish HUD's Scrollbar resets to top whenever content height changes;
    /// restoring the previous pixel offset requires converting it to the
    /// scrollbar's 0..1 travel ratio against the CURRENT content height.
    /// </summary>
    internal static class ScrollMath
    {
        /// <summary>
        /// The scrollbar travel ratio (0..1) that puts the same content
        /// pixel at the top of the viewport. Returns 0 when the content
        /// fits the viewport (nothing to scroll).
        /// </summary>
        public static float RatioForOffset(int savedOffsetPx, int contentHeight, int viewportHeight)
        {
            int scrollable = contentHeight - viewportHeight;
            if (scrollable <= 0 || savedOffsetPx <= 0)
            {
                return 0f;
            }

            float ratio = (float)savedOffsetPx / scrollable;
            return ratio > 1f ? 1f : ratio;
        }

        /// <summary>
        /// Applies a pixel-space scroll delta to a scrollbar's current
        /// ratio, returning the resulting ratio (wheel-wrap fix -
        /// KNOWN-ISSUES #12 reopened). Blish's own Scrollbar.
        /// HandleWheelScroll/ScrollAnimated operate in pixel space (a
        /// fixed per-notch pixel step added to the current pixel offset),
        /// not ratio space directly, so correcting a wrapped multi-notch
        /// wheel event has to convert to pixels, apply the delta, and
        /// convert back - working in ratio space directly would not
        /// compose the same way across a changing scrollable range.
        /// </summary>
        /// <param name="currentRatio">
        /// The scrollbar's current ScrollDistance (0..1); clamped
        /// defensively before use.
        /// </param>
        /// <param name="deltaPixels">
        /// The pixel-space movement to apply (negative moves toward the
        /// top, positive toward the bottom, matching Blish's own
        /// ScrollAnimated pixel convention).
        /// </param>
        /// <param name="contentHeight">The panel's total content height.</param>
        /// <param name="viewportHeight">The panel's visible height.</param>
        /// <returns>The resulting ratio, clamped to 0..1.</returns>
        public static float ApplyPixelDelta(float currentRatio, int deltaPixels, int contentHeight, int viewportHeight)
        {
            int scrollable = contentHeight - viewportHeight;
            if (scrollable <= 0)
            {
                return 0f;
            }

            float clampedRatio = currentRatio < 0f ? 0f : (currentRatio > 1f ? 1f : currentRatio);
            int currentOffsetPx = (int)System.Math.Round(clampedRatio * scrollable);
            int correctedOffsetPx = currentOffsetPx + deltaPixels;

            return RatioForOffset(correctedOffsetPx, contentHeight, viewportHeight);
        }
    }
}
