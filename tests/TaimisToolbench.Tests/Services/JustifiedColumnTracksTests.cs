using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class JustifiedColumnTracksTests
    {
        [Fact]
        public void AdjacentTracks_ShareAnEdgeExactly()
        {
            const int startX = 40;
            const int span = 1000;
            const int tracks = 6;

            for (int i = 0; i < tracks - 1; i++)
            {
                Assert.Equal(
                    JustifiedColumnTracks.RightEdge(startX, span, tracks, i),
                    JustifiedColumnTracks.LeftEdge(startX, span, tracks, i + 1));
            }
        }

        [Fact]
        public void TheLastTrack_EndsExactlyOnTheSpan_NotARoundingPixelShort()
        {
            // 1000 / 7 does not divide; accumulating a rounded track width
            // would strand pixels at the right edge.
            Assert.Equal(
                40 + 1000,
                JustifiedColumnTracks.RightEdge(40, 1000, 7, 6));
        }

        [Fact]
        public void TrackWidths_SumToTheWholeSpan()
        {
            int total = 0;
            for (int i = 0; i < 7; i++)
            {
                total += JustifiedColumnTracks.Width(40, 1000, 7, i);
            }

            Assert.Equal(1000, total);
        }

        [Fact]
        public void CenteredContent_SitsMidTrack()
        {
            // Track 1 of 4 over a 400px span from 0 spans 100..200.
            Assert.Equal(125, JustifiedColumnTracks.CenteredX(0, 400, 4, 1, 50));
        }

        [Fact]
        public void AHeaderAndItsCell_CentreOnTheSameAxis()
        {
            // The point of the law: a narrow header and a wide cell in the
            // same track share a centre line, so the header sits over the
            // values it names.
            int header = JustifiedColumnTracks.CenteredX(0, 400, 4, 2, 40);
            int cell = JustifiedColumnTracks.CenteredX(0, 400, 4, 2, 90);

            Assert.Equal(header + (40 / 2), cell + (90 / 2));
        }

        [Fact]
        public void ContentWiderThanItsTrack_PinsLeft_RatherThanOverhangingBothNeighbours()
        {
            Assert.Equal(
                JustifiedColumnTracks.LeftEdge(0, 400, 4, 1),
                JustifiedColumnTracks.CenteredX(0, 400, 4, 1, 500));
        }

        [Fact]
        public void ZeroTracks_DegradeToTheOrigin_RatherThanDividingByZero()
        {
            Assert.Equal(40, JustifiedColumnTracks.LeftEdge(40, 1000, 0, 0));
            Assert.Equal(40, JustifiedColumnTracks.RightEdge(40, 1000, 0, 0));
            Assert.Equal(0, JustifiedColumnTracks.Width(40, 1000, 0, 0));
            Assert.Equal(40, JustifiedColumnTracks.CenteredX(40, 1000, 0, 0, 10));
        }

        [Fact]
        public void FitsDistributed_IsFalse_WhenATrackCannotHoldItsWidestBandPlusTheGap()
        {
            Assert.True(JustifiedColumnTracks.FitsDistributed(600, 6, 80, 12));
            Assert.False(JustifiedColumnTracks.FitsDistributed(600, 6, 90, 12));
        }

        [Fact]
        public void CenteredInBand_IsTheSameLawAgainstOneColumnsOwnBand()
        {
            // The form every table's HEADER uses: the band, not an equal
            // share of the row, is what the word has to sit over.
            Assert.Equal(155, JustifiedColumnTracks.CenteredInBand(100, 150, 40));
            // A band is a one-track row: the two forms must not drift apart.
            Assert.Equal(
                JustifiedColumnTracks.CenteredX(100, 150, 1, 0, 40),
                JustifiedColumnTracks.CenteredInBand(100, 150, 40));
        }

        [Fact]
        public void CenteredInBand_ContentAtLeastAsWideAsTheBand_PinsLeft()
        {
            Assert.Equal(100, JustifiedColumnTracks.CenteredInBand(100, 150, 150));
            Assert.Equal(100, JustifiedColumnTracks.CenteredInBand(100, 150, 400));
            Assert.Equal(100, JustifiedColumnTracks.CenteredInBand(100, 0, 40));
        }

        // --- CenteredOverContent (the header law: the word sits over the
        // ink, not over the invisible reserve the ink sits in) ---
        [Fact]
        public void CenteredOverContent_LeftRuledColumn_IgnoresTheReserveThatContentDoesNotFill()
        {
            // A 150px band whose badges only ever reach 60px: the header
            // centres on the badges' own 100..160 extent, not on the band's
            // 100..250. Centring in the band would put it 45px right of the
            // ink, which is the defect this exists to fix.
            Assert.Equal(120, JustifiedColumnTracks.CenteredOverContent(100, 150, 100, 60, 20));
            Assert.Equal(165, JustifiedColumnTracks.CenteredInBand(100, 150, 20));
        }

        [Fact]
        public void CenteredOverContent_ContentFillingItsBand_AgreesWithCenteredInBand()
        {
            // The band form is the special case where the reserve IS the
            // content; the two must not answer differently there.
            Assert.Equal(
                JustifiedColumnTracks.CenteredInBand(100, 150, 40),
                JustifiedColumnTracks.CenteredOverContent(100, 150, 100, 150, 40));
        }

        [Fact]
        public void CenteredOverContent_HeaderWiderThanTheContent_PinsToTheBandsNearEdge()
        {
            // Left-ruled: the word would start left of the band, which is
            // the neighbouring column's pixels.
            Assert.Equal(100, JustifiedColumnTracks.CenteredOverContent(100, 150, 100, 10, 60));

            // Right-aligned: it would end past the column's own right edge,
            // i.e. in the panel margin. Clamping there right-aligns the
            // header on that edge, which is where the plain edge rule put
            // it before this law existed.
            Assert.Equal(
                190,
                JustifiedColumnTracks.CenteredOverContentRightAligned(250, 150, 10, 60));
        }

        [Fact]
        public void CenteredOverContent_NoContentAtAll_DegeneratesToTheBandsNearEdge()
        {
            // An empty table, or a column every row leaves blank: there is
            // no ink to centre over and the clamp decides.
            Assert.Equal(100, JustifiedColumnTracks.CenteredOverContent(100, 150, 100, 0, 40));
            Assert.Equal(
                210,
                JustifiedColumnTracks.CenteredOverContentRightAligned(250, 150, 0, 40));
        }

        [Fact]
        public void CenteredOverContentRightAligned_IsTheRightAlignedRestatement()
        {
            // Numbers grow leftward off a shared right edge, so their
            // extent is rightEdge - widest .. rightEdge. One row, or a
            // hundred, the widest is what the header centres on.
            Assert.Equal(
                JustifiedColumnTracks.CenteredOverContent(100, 150, 190, 60, 20),
                JustifiedColumnTracks.CenteredOverContentRightAligned(250, 150, 60, 20));
        }

        [Fact]
        public void CenteredOverContent_ContentWiderThanItsBand_StaysInsideTheBand()
        {
            // A row wider than the column reserved for it - the header must
            // not follow it out into the neighbour.
            int x = JustifiedColumnTracks.CenteredOverContent(100, 150, 100, 400, 40);

            Assert.InRange(x, 100, 100 + 150 - 40);
        }
    }
}
