using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// The only way a header band is built in this module. Two tiers ship a
    /// band, and both are drawn from here: the TAB TITLE band a tab wears at
    /// the top of its view, and the COLUMN HEADER band a table wears above
    /// its rows. Section titles are the third tier and deliberately carry no
    /// band at all - a rule and the SectionTitle face - so the tab title
    /// still reads as the top of the hierarchy when six section headings are
    /// stacked in one scroll.
    /// <para>
    /// This is a factory, not a vocabulary: the colour and the texture are
    /// private here, so there is nothing for a ninth call site to hand-roll
    /// a band out of.
    /// </para>
    /// Why a band at all, and what the opt-in-helper predecessor cost:
    /// docs/ARCHITECTURE.md, "Views: relocated design narrative".
    /// </summary>
    internal static class HeaderBands
    {
        /// <summary>
        /// The grunge strip Blish_HUD.Controls.Panel paints behind its own
        /// title header. 512x32 RGBA: dark noise with a left-to-right alpha
        /// ramp (255 at x=0 down to ~53 at x=480) and no vertical structure
        /// beyond about five rows of edge shading top and bottom.
        /// </summary>
        private const int BandTextureAssetId = 1032325;

        /// <summary>
        /// Opaque base painted UNDER the texture. Control.Draw fills
        /// BackgroundColor before Panel.PaintBeforeChildren blits
        /// BackgroundTexture, so the two compose. The base is load-bearing,
        /// not decoration: the texture's alpha falls to ~53/255 at its right
        /// end, so a texture-only band would go half-transparent over the
        /// window backdrop.
        /// <para>
        /// Exposed because StickyHeaderHost paints the strip between a
        /// pinned band's bottom and the viewport's cutoff with it, so
        /// nothing shows through there at the UI Sizes whose clip round
        /// trip loses pixels.
        /// </para>
        /// </summary>
        internal static readonly Color BandColor = new Color(35, 35, 35);

        private static AsyncTexture2D _bandTexture;

        internal static readonly Color LabelColor = Color.White;

        /// <summary>
        /// Height of a column-header band. Aliased to the height-math
        /// constant rather than duplicated: a header that draws at one
        /// height and is measured at another is how a section's rows drift
        /// off their container.
        /// </summary>
        internal const int RowHeight = PlanContentHeightMath.ColumnHeaderRowHeight;

        /// <summary>
        /// Baseline y of every header label inside that band. Aliased to
        /// the height-math constant for the same reason
        /// <see cref="RowHeight"/> is: the two are one piece of arithmetic.
        /// </summary>
        internal const int LabelY = PlanContentHeightMath.ColumnHeaderLabelY;

        /// <summary>Height of a tab title band, aliased for the same
        /// reason.</summary>
        internal const int TabTitleHeight = PlanContentHeightMath.TabTitleBandHeight;

        /// <summary>
        /// Column headers used to be the same size and weight as the rows
        /// under them, with only the dark band to separate them. This is
        /// the single seam that promotes every table at once.
        /// </summary>
        internal static BitmapFont Font => UiFonts.ColumnHeader;

        /// <summary>
        /// The tier-1 band: one per tab, carrying that tab's only title.
        /// The tab strip on the left of the window draws icons only -
        /// Blish_HUD.Controls.Tab.Draw renders no text and TabbedWindow2
        /// never sets a subtitle - so this band is the one place a tab is
        /// named, which is why it is the module's own and not Blish's 36px
        /// DefaultFont16 header.
        /// </summary>
        internal static Panel CreateTabTitleBand(Container parent, int width, string title, int titleX)
        {
            var band = Band(parent, width, TabTitleHeight, 0, 0);

            new Label()
            {
                Text = title ?? "",
                Font = UiFonts.SectionTitle,
                TextColor = LabelColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(titleX, PlanContentHeightMath.TabTitleY),
                Parent = band,
            };

            return band;
        }

        /// <summary>
        /// The tier-3 band, at the flow position its parent gives it.
        /// </summary>
        internal static Panel CreateColumnHeaderBand(Container parent, int width)
        {
            return Band(parent, width, RowHeight, 0, 0);
        }

        /// <summary>
        /// A band and the fixed-height panel that holds its place in a
        /// FlowPanel. A FlowPanel measures its own children, so a band
        /// lifted straight out of one to be pinned
        /// (Views/Rendering/StickyHeaderHost) would close the flow up by
        /// <see cref="RowHeight"/> and move every section below it; the
        /// spacer is what the flow measures instead, and it stays put
        /// whether the band is inside it or inside the sticky clip.
        /// </summary>
        internal readonly struct FlowBand
        {
            /// <summary>The band itself: the control carrying the labels
            /// and sort cells, and the one that gets re-parented.</summary>
            public readonly Panel Band;

            /// <summary>Its place in the flow, and its parent whenever it
            /// is not pinned.</summary>
            public readonly Panel Spacer;

            internal FlowBand(Panel band, Panel spacer)
            {
                Band = band;
                Spacer = spacer;
            }

            /// <summary>Both halves take one width: a Blish container
            /// clips its children to its own bounds, so a spacer narrower
            /// than its band would cut the band's right end off.</summary>
            internal void Resize(int width)
            {
                var size = new Point(width, RowHeight);
                Spacer.Size = size;
                Band.Size = size;
            }
        }

        /// <summary>
        /// The tier-3 band for a table that flows: see
        /// <see cref="FlowBand"/> for what the extra panel buys.
        /// </summary>
        internal static FlowBand CreateColumnHeaderBandInFlow(Container parent, int width)
        {
            // Wheel-transparent and clipped for the same two reasons the
            // band is - it is one more container in the viewport's chain,
            // and a plain Panel there re-opens the clip drift
            // Views/Rendering/ClipCutoff.cs closes.
            var spacer = new WheelTransparentClippedPanel()
            {
                Size = new Point(width, RowHeight),
                Parent = parent,
            };

            return new FlowBand(Band(spacer, width, RowHeight, 0, 0), spacer);
        }

        /// <summary>
        /// The tier-3 band at an explicit offset, for the tabs that place
        /// their chrome rows absolutely rather than flowing them.
        /// </summary>
        internal static Panel CreateColumnHeaderBand(Container parent, int width, int x, int y)
        {
            return Band(parent, width, RowHeight, x, y);
        }

        private static Panel Band(Container parent, int width, int height, int x, int y)
        {
            // Wheel-transparent because a band can be PINNED over the
            // scrolling panel it belongs to (Views/Rendering/StickyHeaderHost),
            // and every container between that host's clip and the cursor has
            // to pass the wheel through or the walk breaks inside it.
            return new WheelTransparentClippedPanel()
            {
                Size = new Point(width, height),
                Location = new Point(x, y),
                BackgroundColor = BandColor,
                BackgroundTexture = BandTexture,
                Parent = parent,
            };
        }

        /// <summary>
        /// Resolved on first use rather than in a static initialiser:
        /// AsyncTexture2D.FromAssetId reaches into GameService.Content, so
        /// binding it to class load would tie the whole type to a live
        /// Blish content service. Cached the same way UiFonts caches its
        /// faces - DatAssetCache already hands every Panel in the process
        /// the same handle to this asset id, so holding one costs no extra
        /// texture memory.
        /// </summary>
        private static AsyncTexture2D BandTexture =>
            _bandTexture ?? (_bandTexture = AsyncTexture2D.FromAssetId(BandTextureAssetId));
    }
}
