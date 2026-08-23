using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using System.Runtime.CompilerServices;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The module's single choke point for tooltips. Sizing, wrapping,
    /// placement and opacity are solved here once instead of being
    /// re-implemented, or silently not implemented, at each of the ~40
    /// call sites that show one.
    ///
    /// Two paths, and the choice between them is about the CONTENT, not
    /// the caller:
    /// <list type="bullet">
    /// <item><description><see cref="ApplyPlain"/> - composed or long
    /// prose. Routed through <see cref="TooltipTextFormat"/> (the wrap seam
    /// this facility inherits from the tier-1 tooltip work) and handed to
    /// Blish's <c>BasicTooltipText</c>. A bare one-line literal - a button
    /// label - does not need the facility and may stay a direct
    /// assignment.</description></item>
    /// <item><description><see cref="ApplyRich"/> - anything containing a
    /// coin amount, which a string tooltip can only spell out as
    /// "1g 23s 45c". Rendered by <see cref="RichTooltipSurface"/> with real
    /// coin icons (RIGHT of their numbers), an opaque background, and a
    /// four-edge screen clamp Blish's own tooltip positioning does not
    /// have.</description></item>
    /// </list>
    ///
    /// LIFECYCLE (measured, see docs/KNOWN-ISSUES.md "Tooltip facility"):
    /// there is exactly ONE rich surface for the whole module, repointed on
    /// hover. <c>Control.Dispose</c> does not dispose the control's
    /// <c>Tooltip</c>, and the Tooltip is not the control's child, so
    /// nothing in Blish ever tears one down; a per-control instance on
    /// controls this module rebuilds on every render would leak one
    /// container plus its child tree per row per render. Content is held in
    /// a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed by the
    /// control, so the facility never holds a control alive and a disposed
    /// row's content is collected with it.
    /// </summary>
    internal static class TooltipFacility
    {
        private static readonly ConditionalWeakTable<Control, TooltipContent> Contents =
            new ConditionalWeakTable<Control, TooltipContent>();

        private static RichTooltipSurface _surface;

        /// <summary>
        /// Wraps composed prose and assigns it as a plain tooltip. A null
        /// or empty text clears the tooltip, which is what every caller
        /// already means by "no tooltip here".
        /// </summary>
        internal static void ApplyPlain(Control control, string text)
        {
            if (control == null)
            {
                return;
            }
            control.BasicTooltipText = string.IsNullOrEmpty(text) ? null : TooltipTextFormat.Wrap(text);
        }

        /// <summary>
        /// Points a control at the shared rich surface and registers what
        /// that surface should draw when the control is hovered. Null or
        /// empty content clears both paths so a control can never be left
        /// showing a stale tooltip.
        /// </summary>
        internal static void ApplyRich(Control control, TooltipContent content)
        {
            if (control == null)
            {
                return;
            }

            if (content == null || content.IsEmpty)
            {
                Contents.Remove(control);
                control.Tooltip = null;
                control.BasicTooltipText = null;
                return;
            }

            Contents.Remove(control);
            Contents.Add(control, content);

            // Cleared FIRST and deliberately: Control's BasicTooltipText
            // setter nulls the control's _tooltip field whenever the text
            // changes, so assigning the surface and then any basic text
            // would silently drop the surface.
            control.BasicTooltipText = null;
            control.Tooltip = Surface();
        }

        /// <summary>
        /// Disposes the shared surface at module teardown. Nothing else
        /// owns it - it is parented to the SpriteScreen only while visible.
        /// </summary>
        internal static void Shutdown()
        {
            _surface?.Dispose();
            _surface = null;
        }

        private static RichTooltipSurface Surface()
        {
            return _surface ?? (_surface = new RichTooltipSurface(ResolveContent));
        }

        private static TooltipContent ResolveContent(Control control)
        {
            if (control == null)
            {
                return null;
            }
            return Contents.TryGetValue(control, out var content) ? content : null;
        }
    }
}
