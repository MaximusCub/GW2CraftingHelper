using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// Pins a table's column-header band to the top of a scrolling region
    /// while that table's rows are being scrolled past. Blish has no sticky
    /// primitive: a control inside a scrolling Container moves with it, and
    /// a Container clips its children to its own bounds and nothing else.
    /// <para>
    /// So a pinned band is RE-PARENTED into a clip Panel that is a sibling
    /// of the scroll region, sized to the slice that should show. One band,
    /// not a copy: the hover washes, click cells and tooltips are the ones
    /// the table already built, so a pinned header sorts like the header it
    /// is. The clip supplies the top-edge cut Blish will not, and is never
    /// bigger than what it shows.
    /// </para>
    /// <para>
    /// Placement is <see cref="StickyHeaderLayout"/>'s; everything here is
    /// the Blish half - reading live positions, moving controls, and the
    /// per-frame tick that drives it (Blish raises no scroll event).
    /// </para>
    /// </summary>
    internal sealed class StickyHeaderHost
    {
        private static readonly Logger Logger = Logger.GetLogger<StickyHeaderHost>();

        /// <summary>
        /// Above the scrolling panel it overlays. Blish paints a container's
        /// children in ZIndex order, and the clip is a SIBLING of that
        /// panel: without this it would draw underneath whatever the panel
        /// scrolls past it.
        /// </summary>
        private const int ClipZIndex = 1;

        /// <summary>
        /// Where one tracked band sits inside the scrolling content, and how
        /// far its table runs. All in the HOME container's own coordinates,
        /// re-read every frame: the caller's layout owns these, and a table
        /// that is not showing says so with <see cref="Present"/> rather
        /// than by being untracked.
        /// </summary>
        internal readonly struct BandGeometry
        {
            public readonly bool Present;
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;

            /// <summary>One pixel past the table's last row.</summary>
            public readonly int TableBottom;

            public BandGeometry(bool present, int x, int y, int width, int height, int tableBottom)
            {
                Present = present;
                X = x;
                Y = y;
                Width = width;
                Height = height;
                TableBottom = tableBottom;
            }
        }

        private sealed class Entry
        {
            internal Panel Band;
            internal Container Home;
            internal Func<BandGeometry> Geometry;
            internal Panel Clip;
            internal bool Pinned;
        }

        private readonly Container _parent;
        private readonly Container _scrollRegion;
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>Set by the ticker's own failure path; see there.</summary>
        private bool _stopped;

        /// <summary>
        /// <paramref name="parent"/> must NOT scroll - it is where the
        /// pinned clip lives, and a clip that scrolled would defeat the
        /// whole exercise. <paramref name="scrollRegion"/> is the scrolling
        /// panel inside it, whose ContentRegion is the viewport.
        /// </summary>
        internal StickyHeaderHost(Container parent, Container scrollRegion)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _scrollRegion = scrollRegion ?? throw new ArgumentNullException(nameof(scrollRegion));

            // Held only by its parent, which disposes it: this host lives
            // exactly as long as the tab panel it was built for.
            new Ticker(this) { Parent = parent };
        }

        /// <summary>
        /// Tracks one table's band. <paramref name="home"/> is the container
        /// the band belongs to when it is not pinned - the same one whose
        /// live position tells this class where the table has scrolled to,
        /// which is why the band is described in ITS coordinates and not the
        /// viewport's.
        /// </summary>
        internal void Track(Panel band, Container home, Func<BandGeometry> geometry)
        {
            if (band == null || home == null || geometry == null)
            {
                return;
            }

            _entries.Add(new Entry
            {
                Band = band,
                Home = home,
                Geometry = geometry,
                Clip = new Panel()
                {
                    Size = Point.Zero,
                    Visible = false,
                    ZIndex = ClipZIndex,
                    Parent = _parent,
                },
            });
        }

        /// <summary>Drops every tracked band, unpinning first so none is
        /// left orphaned in a clip that is about to go away.</summary>
        internal void Clear()
        {
            foreach (var entry in _entries)
            {
                Unpin(entry, entry.Geometry());
                entry.Clip.Dispose();
            }

            _entries.Clear();
        }

        /// <summary>
        /// One frame's placement for every tracked band. Reads positions and
        /// writes at most a Location and a Size per band, so the common case
        /// - nothing pinned, nothing moved - costs a handful of rectangle
        /// reads.
        /// </summary>
        internal void Update()
        {
            if (_stopped || _entries.Count == 0 || _scrollRegion.Parent == null)
            {
                return;
            }

            var viewport = _scrollRegion.ContentRegion;
            var scrollBounds = _scrollRegion.AbsoluteBounds;
            int viewportTop = scrollBounds.Y + viewport.Y;

            var parentBounds = _parent.AbsoluteBounds;
            var parentRegion = _parent.ContentRegion;
            int originX = parentBounds.X + parentRegion.X;
            int originY = parentBounds.Y + parentRegion.Y;

            for (int i = 0; i < _entries.Count; i++)
            {
                Place(_entries[i], viewportTop, viewport.Height, originX, originY);
            }
        }

        private static void Place(
            Entry entry, int viewportTop, int viewportHeight, int originX, int originY)
        {
            var geometry = entry.Geometry();
            if (!geometry.Present || entry.Home.Parent == null)
            {
                Unpin(entry, geometry);
                return;
            }

            // The home container's own absolute position already carries the
            // scroll, so the band's viewport-relative y falls out of it
            // without this class knowing how Blish applies a scroll offset.
            var homeBounds = entry.Home.AbsoluteBounds;
            var homeRegion = entry.Home.ContentRegion;
            int homeTop = homeBounds.Y + homeRegion.Y;
            int headerY = homeTop + geometry.Y - viewportTop;
            int tableBottomY = homeTop + geometry.TableBottom - viewportTop;

            var placement = StickyHeaderLayout.Compute(
                headerY, geometry.Height, tableBottomY, viewportHeight);

            if (!placement.Pinned)
            {
                Unpin(entry, geometry);
                return;
            }

            int clipX = homeBounds.X + homeRegion.X + geometry.X;
            var clipLocation = new Point(clipX - originX, viewportTop + placement.ClipY - originY);
            var clipSize = new Point(geometry.Width, placement.VisibleHeight);
            if (entry.Clip.Location != clipLocation)
            {
                entry.Clip.Location = clipLocation;
            }

            if (entry.Clip.Size != clipSize)
            {
                entry.Clip.Size = clipSize;
            }

            if (!entry.Pinned)
            {
                entry.Band.Parent = entry.Clip;
                entry.Pinned = true;
            }

            var bandLocation = new Point(0, -placement.OffsetInBand);
            if (entry.Band.Location != bandLocation)
            {
                entry.Band.Location = bandLocation;
            }

            entry.Clip.Visible = true;
        }

        /// <summary>Puts a band back where its own layout wants it. The
        /// caller's geometry is the authority on that, not a location
        /// remembered from before the pin - a resize during a pin would have
        /// moved it.</summary>
        private static void Unpin(Entry entry, BandGeometry geometry)
        {
            entry.Clip.Visible = false;
            if (!entry.Pinned)
            {
                return;
            }

            entry.Band.Parent = entry.Home;
            entry.Pinned = false;
            entry.Band.Location = new Point(geometry.X, geometry.Y);
        }

        /// <summary>
        /// Blish raises nothing when a panel is scrolled, so placement is
        /// re-derived per frame. Zero-sized on purpose: it exists to be
        /// updated, and a control with no area is in no hit test - the same
        /// idiom Views/ModalBackdrop uses when it stands down.
        /// </summary>
        private sealed class Ticker : Control
        {
            private readonly StickyHeaderHost _host;

            internal Ticker(StickyHeaderHost host)
            {
                _host = host;
                Size = Point.Zero;
                Location = Point.Zero;
            }

            public override void DoUpdate(GameTime gameTime)
            {
                try
                {
                    _host.Update();
                }
                catch (Exception ex)
                {
                    // One line, then stood down: this runs every frame, and
                    // a header stuck mid-scroll reads to the user as a
                    // frozen table rather than as an error.
                    _host._stopped = true;
                    Logger.Warn(ex, "Sticky header placement failed; stopping");

                    // At most one line: _stopped is what keeps this from
                    // running again. Not disposed here - a control must not
                    // leave the tree from inside the update walk over it.
                    ModuleLog.Shared.Write(
                        ModuleLogLevel.Warn, "ui",
                        "Sticky table headers stopped after a placement failure: "
                        + ex.GetType().Name + " - " + ex.Message);
                }
            }

            protected override void Paint(
                Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Rectangle bounds)
            {
            }
        }
    }
}
