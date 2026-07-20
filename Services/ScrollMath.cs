namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure scroll-restoration arithmetic (Blish-free, unit-testable).
    /// Blish HUD's Scrollbar resets to top whenever content height changes;
    /// restoring the previous pixel offset requires converting it to the
    /// scrollbar's 0..1 travel ratio against the CURRENT content height.
    /// </summary>
    public static class ScrollMath
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
    }
}
