using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The arithmetic behind "the row under my cursor must not move when a
    /// re-solve reflows the sections above it" - the maintainer's report
    /// that toggling IGNORE jars the view because the Total Cost section
    /// gains or loses currency rows. Each test runs the real
    /// capture-then-restore sequence the view runs, over a candidate list
    /// standing in for the content panel's laid-out children.
    /// </summary>
    public class ScrollAnchorMathTests
    {
        private const int ViewportHeight = 400;

        // Total Cost header at 0, its band, the tree section header, and
        // three tree rows. summaryHeight is what a re-solve changes.
        private static List<ScrollAnchorCandidate> Layout(int summaryHeight)
        {
            int treeTop = summaryHeight;
            return new List<ScrollAnchorCandidate>
            {
                new ScrollAnchorCandidate("section:Summary", 0, summaryHeight),
                new ScrollAnchorCandidate("section:RecipeTree", treeTop, 40),
                new ScrollAnchorCandidate("node:1", treeTop + 40, 30),
                new ScrollAnchorCandidate("node:2", treeTop + 70, 30),
                new ScrollAnchorCandidate("node:3", treeTop + 100, 30)
            };
        }

        private static int ContentHeight(List<ScrollAnchorCandidate> layout)
        {
            int bottom = 0;
            foreach (var c in layout)
            {
                if (c.Top + c.Height > bottom) bottom = c.Top + c.Height;
            }
            return bottom + 2000; // plenty of rows below; never clamps
        }

        [Fact]
        public void SectionAboveGrows_RowUnderTheCursorKeepsItsScreenPosition()
        {
            var before = Layout(summaryHeight: 200);
            // Viewport top at 250, cursor 60px down it: content y 310,
            // which sits in node:3 (300..330 with summaryHeight 200).
            int savedOffset = 250;
            int anchorLine = ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, 60);

            Assert.True(ScrollAnchorMath.TryCapture(before, anchorLine, out var anchor));
            int screenYBefore = anchor.CapturedTop - savedOffset;

            // The re-solve adds two currency rows above everything.
            var after = Layout(summaryHeight: 260);
            int? newTop = ScrollAnchorMath.FindTop(after, anchor);
            Assert.True(newTop.HasValue);

            int restored = ScrollAnchorMath.RestoredOffset(
                savedOffset, anchor, newTop.Value, ContentHeight(after), ViewportHeight);

            Assert.Equal(310, savedOffset + 60);
            Assert.Equal(screenYBefore, newTop.Value - restored);
            // The content above grew by 60, so the viewport follows it by
            // exactly 60 rather than staying put and letting the row slide.
            Assert.Equal(310, restored);
        }

        [Fact]
        public void SectionAboveShrinks_ViewportFollowsUpward()
        {
            var before = Layout(summaryHeight: 260);
            int savedOffset = 300;
            int anchorLine = ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, 40);
            Assert.True(ScrollAnchorMath.TryCapture(before, anchorLine, out var anchor));

            var after = Layout(summaryHeight: 200);
            int? newTop = ScrollAnchorMath.FindTop(after, anchor);
            int restored = ScrollAnchorMath.RestoredOffset(
                savedOffset, anchor, newTop.Value, ContentHeight(after), ViewportHeight);

            Assert.Equal(240, restored);
            Assert.Equal(anchor.CapturedTop - savedOffset, newTop.Value - restored);
        }

        [Fact]
        public void NothingMoves_RestoresTheSameOffset()
        {
            var layout = Layout(summaryHeight: 200);
            int savedOffset = 250;
            int anchorLine = ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, 60);
            Assert.True(ScrollAnchorMath.TryCapture(layout, anchorLine, out var anchor));

            int restored = ScrollAnchorMath.RestoredOffset(
                savedOffset, anchor, ScrollAnchorMath.FindTop(layout, anchor).Value,
                ContentHeight(layout), ViewportHeight);

            Assert.Equal(savedOffset, restored);
        }

        [Fact]
        public void CursorLine_PicksTheRowUnderIt_NotTheViewportTop()
        {
            var layout = Layout(summaryHeight: 200);
            int savedOffset = 250;

            Assert.True(ScrollAnchorMath.TryCapture(
                layout, ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, 60), out var withCursor));
            Assert.True(ScrollAnchorMath.TryCapture(
                layout, ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, null), out var withoutCursor));

            // 310 lands in node:3 (300..330); the viewport top, 250, is
            // still up in node:1 (240..270). The cursor is what the user
            // is reading, so it decides.
            Assert.Equal("node:3", withCursor.Key);
            Assert.Equal("node:1", withoutCursor.Key);
        }

        [Fact]
        public void CursorOutsideTheViewport_FallsBackToTheViewportTop()
        {
            int savedOffset = 250;

            Assert.Equal(savedOffset, ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, null));
            Assert.Equal(savedOffset, ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, -5));
            Assert.Equal(savedOffset, ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, ViewportHeight));
            Assert.Equal(savedOffset + 399, ScrollAnchorMath.AnchorLine(savedOffset, ViewportHeight, 399));
        }

        [Fact]
        public void NestedCandidates_PickTheMostSpecificOneOnTheLine()
        {
            // A row that starts exactly where its section header does: the
            // shorter (deeper) element wins, so the anchor tracks the row
            // rather than the whole section.
            var layout = new List<ScrollAnchorCandidate>
            {
                new ScrollAnchorCandidate("section:RecipeTree", 100, 500),
                new ScrollAnchorCandidate("node:9", 100, 30)
            };

            Assert.True(ScrollAnchorMath.TryCapture(layout, 110, out var anchor));
            Assert.Equal("node:9", anchor.Key);
        }

        [Fact]
        public void AnchoredRowDisappears_CallerLearnsToFallBack()
        {
            // Ignoring a node drops its whole subtree; the row the user was
            // on can be one of them. FindTop returns null rather than
            // guessing, which is what puts the view back on plain offset
            // preservation.
            var before = Layout(summaryHeight: 200);
            Assert.True(ScrollAnchorMath.TryCapture(before, 310, out var anchor));
            Assert.Equal("node:3", anchor.Key);

            var after = new List<ScrollAnchorCandidate>
            {
                new ScrollAnchorCandidate("section:Summary", 0, 200),
                new ScrollAnchorCandidate("section:RecipeTree", 200, 40),
                new ScrollAnchorCandidate("node:1", 240, 30)
            };

            Assert.Null(ScrollAnchorMath.FindTop(after, anchor));
        }

        [Fact]
        public void LineAboveEveryCandidate_CapturesNothing()
        {
            var layout = new List<ScrollAnchorCandidate>
            {
                new ScrollAnchorCandidate("section:Summary", 50, 200)
            };

            Assert.False(ScrollAnchorMath.TryCapture(layout, 20, out var anchor));
            Assert.False(anchor.IsValid);
        }

        [Fact]
        public void EmptyOrNullCandidates_CaptureNothingAndFindNothing()
        {
            Assert.False(ScrollAnchorMath.TryCapture(null, 100, out _));
            Assert.False(ScrollAnchorMath.TryCapture(new List<ScrollAnchorCandidate>(), 100, out _));
            Assert.Null(ScrollAnchorMath.FindTop(null, new ScrollAnchor("node:1", 0)));
            Assert.Null(ScrollAnchorMath.FindTop(Layout(200), default(ScrollAnchor)));
        }

        [Fact]
        public void KeylessCandidatesAreIgnored()
        {
            // A control the view could not key (nothing registers one
            // today) must never become the anchor: an anchor nobody can
            // re-find after the rebuild is worse than none.
            var layout = new List<ScrollAnchorCandidate>
            {
                new ScrollAnchorCandidate("section:Summary", 0, 100),
                new ScrollAnchorCandidate(null, 90, 10),
                new ScrollAnchorCandidate("", 95, 10)
            };

            Assert.True(ScrollAnchorMath.TryCapture(layout, 99, out var anchor));
            Assert.Equal("section:Summary", anchor.Key);
        }

        [Fact]
        public void RestoreClampsToWhatTheShrunkContentCanScrollTo()
        {
            // Everything below the anchor collapsed away, so the content
            // no longer reaches the offset the anchor asks for.
            var anchor = new ScrollAnchor("node:2", 300);
            int restored = ScrollAnchorMath.RestoredOffset(
                savedOffset: 250, anchor: anchor, newAnchorTop: 300,
                contentHeight: 500, viewportHeight: ViewportHeight);

            Assert.Equal(100, restored);
        }

        [Fact]
        public void RestoreNeverGoesNegativeOrScrollsUnscrollableContent()
        {
            var anchor = new ScrollAnchor("node:2", 300);

            Assert.Equal(0, ScrollAnchorMath.RestoredOffset(
                savedOffset: 50, anchor: anchor, newAnchorTop: 100,
                contentHeight: 5000, viewportHeight: ViewportHeight));

            // Content shorter than the viewport cannot scroll at all.
            Assert.Equal(0, ScrollAnchorMath.RestoredOffset(
                savedOffset: 250, anchor: anchor, newAnchorTop: 300,
                contentHeight: 100, viewportHeight: ViewportHeight));
        }
    }
}
