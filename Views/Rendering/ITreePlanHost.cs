using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Everything <see cref="TreeSectionController"/> needs from
    /// CraftingPlanView beyond relayout registration (which is
    /// <see cref="ISectionRelayoutSink"/>'s job): the scroll-preserving
    /// mutation wrapper, the status line, the post-re-solve render entry
    /// point, the current plan and panel width, the shared section chrome,
    /// and the toolbar publication seam.
    ///
    /// One named interface rather than a list of constructor delegates:
    /// the callbacks are semantically one collaborator, and two of them
    /// used to share the type <c>Action&lt;PlanViewModel&gt;</c> with
    /// opposite meanings (render vs. assign-field), so transposing them
    /// compiled. Named members make that swap unexpressible and give a new
    /// tree feature one place to grow instead of four.
    ///
    /// Implemented explicitly by CraftingPlanView, which already implements
    /// <see cref="ISectionRelayoutSink"/> the same way, so nothing here
    /// widens that class's public surface. The header member returns a
    /// ValueTuple because CraftingPlanView's own SectionHeaderHandle is a
    /// private nested type.
    /// <para>See docs/ARCHITECTURE.md section 5.</para>
    /// </summary>
    internal interface ITreePlanHost
    {
        /// <summary>
        /// Runs <paramref name="mutate"/> with the content panel's scroll
        /// offset captured before and restored after - KNOWN-ISSUES #39.
        /// Every tree mutation that changes content height (expand,
        /// collapse, re-solve) goes through it.
        /// </summary>
        void PreserveScrollAcross(Action mutate);

        /// <summary>Writes the strip's status line.</summary>
        void SetStatus(string status);

        /// <summary>
        /// The render a local re-solve runs: refresh the tree in place if
        /// it can, otherwise rebuild. Deliberately NOT the host's plain
        /// full-render entry point - the tree's own re-solve path is the
        /// one case that may keep the built tree.
        /// </summary>
        void RenderPlanAfterResolve(PlanViewModel vm);

        /// <summary>
        /// The view model currently rendered. The override loop reads it
        /// for currency metadata and row pairing, and replaces it after a
        /// re-solve.
        /// </summary>
        PlanViewModel CurrentPlan { get; set; }

        /// <summary>
        /// The content width the plan is laid out at RIGHT NOW, never a
        /// build-time capture - see CraftingPlanView.GetCurrentPanelWidth.
        /// </summary>
        int PanelWidth { get; }

        /// <summary>
        /// Adopts the debug log a re-solve produced, so the Log tab shows
        /// the solve the user is actually looking at.
        /// </summary>
        void SetLastDebugLog(IReadOnlyList<string> log);

        /// <summary>
        /// The shared collapsible-section chrome, built into the tree's own
        /// relayout registry rather than the view's - the tree's chrome
        /// must survive a preserving re-render that clears the view's.
        /// That routing is the host's business, which is why it is not a
        /// parameter here.
        /// </summary>
        (Panel HeaderPanel, Label ArrowLabel, FlowPanel ContentFlow) CreateTreeSectionHeader(
            string title, PlanSectionType sectionKey, int panelWidth, bool defaultExpanded,
            Func<bool> suppressToggle);

        /// <summary>
        /// Publishes (or, with null, withdraws) the five tree actions to
        /// whatever surface hosts their buttons - see
        /// <see cref="TreeToolbarCommands"/>.
        /// </summary>
        void SetTreeToolbar(TreeToolbarCommands commands);
    }
}
