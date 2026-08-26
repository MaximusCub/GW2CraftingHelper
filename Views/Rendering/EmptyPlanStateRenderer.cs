using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// What the plan tab draws when it holds no plan. Stateless and
    /// freshly constructed per render, like the section renderers.
    /// <para>
    /// Emptying the content panel first is deliberately NOT this
    /// renderer's job: the caller has to reach the same "nothing rendered
    /// yet" point RenderPlan builds from - which also clears the very
    /// registry this renderer then writes into - and that reset is bound
    /// up with tree render state and the scroll anchors, both of which
    /// live on the view.
    /// </para>
    /// </summary>
    internal sealed class EmptyPlanStateRenderer
    {
        // What the tab says when it holds no plan. The default state was
        // blank parchment plus a small "Ready" on the status strip, which
        // names no next action - the Log tab already answers the same
        // question with a dim label in its own empty content panel, and
        // this is that pattern.
        private const string EmptyPlanText =
            "No plan yet. Search for an item above, then click Generate Plan.";

        private const int EmptyPlanTopGap = 48;
        private static readonly Color EmptyPlanTextColor = new Color(150, 150, 150);

        private readonly ISectionRelayoutSink _sink;

        internal EmptyPlanStateRenderer(ISectionRelayoutSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>
        /// Parents the empty-state label into the (already emptied) content
        /// panel. Nothing disposes it explicitly: it is a child of the
        /// content panel like every rendered section, so
        /// ResetContentPanelToEmpty sweeps it on the first render of a real
        /// plan - which is the "disposed on first render" the finding asks
        /// for, through the path that already exists rather than a second
        /// one that could drift from it.
        /// <para>
        /// The gap is a spacer Panel, not a Location: the content panel is
        /// a SingleTopToBottom FlowPanel and positions its own children,
        /// the same reason CreateSectionHeader emits a topGap panel.
        /// </para>
        /// </summary>
        internal void Render(FlowPanel contentPanel, int panelWidth)
        {
            var topGap = new Panel()
            {
                Size = new Point(panelWidth, EmptyPlanTopGap),
                Parent = contentPanel,
            };

            var label = new Label()
            {
                Font = UiFonts.Body,
                Text = EmptyPlanText,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = panelWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = EmptyPlanTextColor,
                Parent = contentPanel,
            };

            _sink.AddRelayout(w =>
            {
                int width = w > 0 ? w : 0;
                topGap.Size = new Point(width, EmptyPlanTopGap);
                label.Width = width;
            });
        }
    }
}
