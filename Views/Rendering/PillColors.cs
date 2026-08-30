using Microsoft.Xna.Framework;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    // GetPillColors lives here rather than on either caller because it is
    // shared by the Shopping List section's source-tag panel and by
    // RenderDecisionPills. Making it internal on CraftingPlanView instead
    // would point a Views/Rendering type back at the view; dependencies run
    // one way (docs/ARCHITECTURE.md section 5).
    internal static class PillColors
    {
        /// <summary>
        /// Pill kinds that are chrome, never an affordance: they annotate a
        /// row, and clicking them has never done anything. They are styled
        /// deliberately unlike a clickable pill (recessed border, dimmed
        /// label) so "can I click this?" is answerable without hovering.
        /// PillKind.Subdued is NOT here - it looks muted but stays fully
        /// clickable (see DecisionPillPlanner's own note on it).
        /// </summary>
        internal static bool IsNonInteractiveChrome(PillKind kind)
        {
            return kind == PillKind.Locked;
        }

        /// <summary>
        /// Label alpha for a non-interactive pill. Below white, but well
        /// above the 0.35 a dimmed row's own name/value labels use - this
        /// text must still be readable at rest, it just must not read as
        /// the same tier as a pill you can act on.
        /// </summary>
        internal const float NonInteractiveTextAlpha = 0.78f;

        /// <summary>
        /// Alpha floor for a DIMMED row's pill border/fill/label. The
        /// reference-branch dim factor (0.35, still used for that row's
        /// name/quantity/cost) crushed every pill hue toward the same
        /// near-black ring, so a dimmed row's pill set was unreadable as a
        /// set - which is the opposite of what dimming should say ("this
        /// whole branch is inactive"). At 0.6 the hues survive, and the
        /// row's 0.35 text, neutral icon frame and icon scrim do the
        /// "one inactive block" work the crush was doing badly.
        /// </summary>
        internal const float DimmedPillFactor = 0.6f;

        /// <summary>
        /// The IGNORE toggle's mark, which inverts with the key under it:
        /// near-black punched into the filled ON key (4.19:1 against that
        /// amber), and the white every other interactive pill's label
        /// draws when the key is OFF and unfilled. The pill's own
        /// <see cref="GetPillColors"/> arm carries the rest of the
        /// raised/pressed pair.
        /// <para>
        /// The punch-out is a FULL-STRENGTH affordance only.
        /// <see cref="DimmedPillFactor"/> multiplies both the mark and the
        /// key toward black, which compresses that 4.19:1 to 2.17:1 - below
        /// the 3:1 non-text minimum, on a row a tree-wide ignore can
        /// perfectly well reach (ignores are keyed by item id, so an
        /// occurrence under a bought parent draws the ON key too). A dimmed
        /// ON key therefore keeps the light mark, which reads 3.05:1
        /// against it and still says "filled key" - the state signal is the
        /// fill, not the inversion.
        /// </para>
        /// </summary>
        internal static Color GlyphColor(bool isIgnoreActive, bool dimmed)
        {
            return isIgnoreActive && !dimmed ? new Color(28, 20, 6) : Color.White; // #1C1406
        }

        /// <summary>
        /// isIgnoreActive is only meaningful for PillKind.Ignore (whether
        /// THIS specific Ignore pill is the active/"IGNORED" state, i.e.
        /// node.IsIgnored) - ignored for every other kind.
        /// </summary>
        internal static void GetPillColors(PillKind kind, bool isIgnoreActive, out Color border, out Color fill)
        {
            switch (kind)
            {
                case PillKind.Selected:
                    // The original #2DC50E border
                    // measured 2.31:1 against white, below the 3:1 WCAG
                    // non-text contrast minimum. Darkened toward #1F8F0C
                    // (4.21:1) - same hue, same fill*0.15 fill, same white
                    // label text - only the ring
                    // itself changed.
                    border = new Color(31, 143, 12); // #1F8F0C
                    fill = border * 0.15f;
                    break;
                case PillKind.Have:
                    border = new Color(113, 113, 255); // #7171FF
                    fill = border * 0.15f;
                    break;
                case PillKind.Available:
                    border = new Color(138, 138, 138); // #8A8A8A
                    fill = Color.Transparent;
                    break;
                case PillKind.OwnedInfo:
                    // Muted gold, distinct from every other pill hue -
                    // informational only, never confused with a selectable
                    // source. The original
                    // #C9A227 border measured 2.42:1 against white; darkened
                    // to #8A6D1F (4.90:1), same hue.
                    border = new Color(138, 109, 31); // #8A6D1F
                    fill = border * 0.15f;
                    break;
                case PillKind.Ignore:
                    // The module's one two-state TOGGLE, and since the
                    // control stopped carrying a word (it draws
                    // UiGlyphs.RemoveMark in both states) its state has to
                    // be legible from the chrome alone. Raised vs.
                    // pressed, the toolbar-toggle vocabulary: OFF is an
                    // outlined key - crisp grey ring, no fill, matching the
                    // clickable Available pill and never Selected's green
                    // - and ON is that key pushed in, filled solid with the
                    // amber the ring used to draw at #9C7327 (4.29:1
                    // against white) and edged in a darker one so the lit
                    // top edge a raised key shows is gone. The mark itself
                    // inverts with the surface it sits on - see
                    // <see cref="GlyphColor"/>.
                    border = isIgnoreActive ? new Color(94, 69, 23) : new Color(138, 138, 138); // #5E4517 / #8A8A8A
                    fill = isIgnoreActive ? new Color(156, 115, 39) : Color.Transparent; // #9C7327
                    break;
                case PillKind.AchievementBitDeduped:
                    // Muted violet - distinct from Have's blue and
                    // OwnedInfo's gold: nothing here is actually owned, just
                    // already required elsewhere.
                    border = new Color(155, 118, 219); // #9B76DB
                    fill = border * 0.15f;
                    break;
                case PillKind.Subdued:
                    // Was byte-identical to Locked below, kept as its own
                    // arm precisely so a later edit to Locked could not
                    // retint it without a deliberate choice. This is that
                    // choice: Locked became non-interactive chrome and
                    // recessed its ring, while a Subdued pill is still a
                    // real click target and keeps the full-strength muted
                    // grey. The two are now deliberately different.
                    border = new Color(107, 107, 107); // #6B6B6B
                    fill = Color.Black * 0.3f;
                    break;
                case PillKind.Locked:
                default:
                    // Non-interactive chrome (UNKNOWN / UNRECOGNIZED /
                    // CURRENCY / GUILD UPGRADE / the sole-source badge, plus
                    // the Shopping List's source tags). The ring drops to
                    // 45% alpha so it reads as a recessed plate rather than
                    // the crisp full-strength ring an Available pill - which
                    // IS clickable - draws at the same hue family.
                    border = new Color(107, 107, 107) * 0.45f; // #6B6B6B
                    fill = Color.Black * 0.3f;
                    break;
            }
        }
    }
}
