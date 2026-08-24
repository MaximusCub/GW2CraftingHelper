using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using System;
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
    /// "1g 23s 45c", and every item hover. Rendered by
    /// <see cref="RichTooltipSurface"/> with real coin icons (RIGHT of
    /// their numbers), on the game's own tooltip canvas, with a four-edge
    /// screen clamp Blish's own tooltip positioning does not
    /// have.</description></item>
    /// <item><description><see cref="ApplyRichDeferred"/> - the same
    /// surface, for content whose INPUTS can change after the control was
    /// built (an item's stat block arriving from a background fetch) or
    /// that is not worth composing until someone points at the
    /// row.</description></item>
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
        private static readonly Logger Logger = Logger.GetLogger(typeof(TooltipFacility));

        private static readonly ConditionalWeakTable<Control, TooltipContentSource> Contents =
            new ConditionalWeakTable<Control, TooltipContentSource>();

        private static RichTooltipSurface _surface;

        /// <summary>
        /// What a control's rich tooltip IS: either finished content, or a
        /// function that builds it when the box is about to be drawn.
        /// <para>
        /// The deferred half exists for one reason: an item's stat block
        /// can land AFTER its row was rendered (a plan restored from disk
        /// fetches its stats in the background - Q13), and a snapshot of
        /// the content taken at render time can never show them. Resolving
        /// at hover time also moves the compose work off the render path,
        /// where it ran once per row per render for rows nobody points at.
        /// </para>
        /// </summary>
        private sealed class TooltipContentSource
        {
            private readonly TooltipContent _content;
            private readonly Func<TooltipContent> _build;

            internal TooltipContentSource(TooltipContent content)
            {
                _content = content;
            }

            internal TooltipContentSource(Func<TooltipContent> build)
            {
                _build = build;
            }

            /// <summary>
            /// What the control said before this facility took its tooltip
            /// over - the missing-icon note, a currency name. Deferred
            /// content cannot be inspected before the control is stamped,
            /// and a builder that turns out to have nothing to say would
            /// otherwise leave the control silent where it used to be
            /// informative.
            /// </summary>
            internal string FallbackText { get; set; }

            internal TooltipContent Resolve()
            {
                return TooltipContent.OrText(_content ?? _build(), FallbackText);
            }
        }

        /// <summary>
        /// Wraps composed prose and assigns it as a plain tooltip. A null
        /// or empty text clears the tooltip, which is what every caller
        /// already means by "no tooltip here".
        /// <para>
        /// The same call also records the caller's intent as the rich
        /// fallback of whatever source the control already carries, CLEARS
        /// INCLUDED. Without it a deliberate clear could not be told from
        /// the null <see cref="Register"/> itself writes, and the next
        /// re-stamp would carry a note the caller had just retracted back
        /// in - a row whose label now fits would show its own full text as
        /// a tooltip over the text it is already showing in full.
        /// </para>
        /// </summary>
        internal static void ApplyPlain(Control control, string text)
        {
            if (control == null)
            {
                return;
            }

            string wrapped = string.IsNullOrEmpty(text) ? null : TooltipTextFormat.Wrap(text);
            if (Contents.TryGetValue(control, out var source))
            {
                source.FallbackText = wrapped;
            }
            control.BasicTooltipText = wrapped;
        }

        /// <summary>
        /// Points a control at the shared rich surface and registers what
        /// that surface should draw when the control is hovered. Null or
        /// empty content clears both paths so a control can never be left
        /// showing a stale tooltip.
        /// </summary>
        internal static void ApplyRich(Control control, TooltipContent content)
        {
            if (content == null || content.IsEmpty)
            {
                Clear(control);
                return;
            }
            Register(control, new TooltipContentSource(content));
        }

        /// <summary>
        /// Same as <see cref="ApplyRich"/>, except the content is composed
        /// when the tooltip is about to be SHOWN rather than now - see
        /// <see cref="TooltipContentSource"/>. A builder that returns empty
        /// content simply shows nothing, so a caller does not have to know
        /// up front whether it has anything to say.
        /// </summary>
        internal static void ApplyRichDeferred(Control control, Func<TooltipContent> build)
        {
            if (build == null)
            {
                Clear(control);
                return;
            }
            Register(control, new TooltipContentSource(build));
        }

        /// <summary>
        /// Redraws whatever the surface is showing right now, for content
        /// whose INPUTS changed without the control being re-stamped - the
        /// background stat top-up landing while the cursor already rests on
        /// a row (Q13). A no-op when nothing is showing. Main thread only,
        /// like every other control mutation.
        /// </summary>
        internal static void RefreshCurrent()
        {
            _surface?.RefreshCurrent();
        }

        private static void Clear(Control control)
        {
            if (control == null)
            {
                return;
            }
            Contents.Remove(control);
            control.Tooltip = null;
            control.BasicTooltipText = null;
            _surface?.RefreshShowing(control);
        }

        private static void Register(Control control, TooltipContentSource source)
        {
            if (control == null)
            {
                return;
            }

            Contents.TryGetValue(control, out var previous);

            // Carried forward on a re-stamp rather than re-read: the
            // control's own text was nulled below on the FIRST stamp, so
            // reading it again would find nothing and lose the note. A
            // caller that has since assigned real plain text wins, because
            // that text is what the control says now - and one that has
            // since CLEARED it wins too, because ApplyPlain wrote that
            // clear onto the previous source rather than leaving it to be
            // inferred from a field this method zeroes.
            source.FallbackText = string.IsNullOrEmpty(control.BasicTooltipText)
                ? previous?.FallbackText
                : control.BasicTooltipText;

            // Remove-then-Add, because net472's ConditionalWeakTable has no
            // AddOrUpdate and Add throws on a duplicate key. A control is
            // usually brand new here (rows and pills are rebuilt per
            // render), so the Remove is normally a miss.
            Contents.Remove(control);
            Contents.Add(control, source);

            // Cleared FIRST and deliberately: Control's BasicTooltipText
            // setter nulls the control's _tooltip field whenever the text
            // changes, so assigning the surface and then any basic text
            // would silently drop the surface.
            control.BasicTooltipText = null;
            var surface = Surface();
            control.Tooltip = surface;

            // Content can be re-applied to a control the surface is ALREADY
            // showing (the tree's settle re-ellipsis does exactly that under a
            // stationary cursor). Blish's plain path refreshes a visible basic
            // tooltip on every text change; the rich path would otherwise keep
            // drawing the previous content until the pointer left.
            surface.RefreshShowing(control);
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
            if (!Contents.TryGetValue(control, out var source))
            {
                return null;
            }

            try
            {
                return source.Resolve();
            }
            catch (Exception ex)
            {
                // A deferred builder runs inside Blish's mouse-moved
                // handler, so an exception here would surface as a crash on
                // hover rather than as a missing tooltip. Degrading to
                // whatever the control said before - usually nothing - is
                // the correct answer, and the log line names the builder
                // that failed.
                Logger.Warn(ex, "Rich tooltip content builder threw; falling back to the control's own text");
                return TooltipContent.OrText(null, source.FallbackText);
            }
        }
    }
}
