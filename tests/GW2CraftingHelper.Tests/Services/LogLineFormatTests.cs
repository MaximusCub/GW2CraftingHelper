using System;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class LogLineFormatTests
    {
        // Local time, so the assertions below can state the exact rendered
        // timestamp without depending on the machine's own zone: the
        // formatter converts UTC -> local, and DateTime.ToLocalTime() on a
        // value already marked Local is the identity.
        private static ModuleLogEntry Entry(string tag, string message)
        {
            return new ModuleLogEntry
            {
                TimestampUtc = new DateTime(2026, 8, 16, 14, 3, 9, DateTimeKind.Local),
                Level = ModuleLogLevel.Warn,
                Tag = tag,
                Message = message
            };
        }

        [Fact]
        public void Prefix_WithTag_LevelUpperTimestampThenTag()
        {
            Assert.Equal("[WARN] 2026-08-16 14:03:09 [snapshot]", LogLineFormat.Prefix(Entry("snapshot", "hi")));
        }

        [Fact]
        public void Prefix_NoTag_OmitsTagBracketsEntirely()
        {
            Assert.Equal("[WARN] 2026-08-16 14:03:09", LogLineFormat.Prefix(Entry(null, "hi")));
            Assert.Equal("[WARN] 2026-08-16 14:03:09", LogLineFormat.Prefix(Entry("", "hi")));
        }

        [Fact]
        public void Line_MatchesTheFlatFormatCopyAndSearchStillUse()
        {
            // The exact strings LogTabContent.FormatLine produced before the
            // row was split into two labels - the Copy button joins these and
            // the search box matches against them, so a drift here is a
            // silent behavior change in both.
            Assert.Equal("[WARN] 2026-08-16 14:03:09 [snapshot] disk full", LogLineFormat.Line(Entry("snapshot", "disk full")));
            Assert.Equal("[WARN] 2026-08-16 14:03:09 disk full", LogLineFormat.Line(Entry(null, "disk full")));
        }

        [Fact]
        public void Line_IsExactlyPrefixSpaceMessage()
        {
            // The split-rendering contract: what the two labels show,
            // rejoined by the one space the message column's x offset draws
            // instead, is the tooltip/copy line - so a truncated row's
            // tooltip can never disagree with what Copy would emit.
            var entry = Entry("plan", "solver ran");
            Assert.Equal(
                LogLineFormat.Prefix(entry) + " " + LogLineFormat.Message(entry),
                LogLineFormat.Line(entry));
        }

        [Fact]
        public void NullMessage_RendersAsEmptyNotNull()
        {
            var entry = Entry("log", null);
            Assert.Equal(string.Empty, LogLineFormat.Message(entry));
            Assert.Equal("[WARN] 2026-08-16 14:03:09 [log] ", LogLineFormat.Line(entry));
        }

        [Fact]
        public void EveryLevel_RendersItsOwnUppercaseName()
        {
            foreach (ModuleLogLevel level in Enum.GetValues(typeof(ModuleLogLevel)))
            {
                var entry = Entry(null, "x");
                entry.Level = level;
                Assert.StartsWith("[" + level.ToString().ToUpperInvariant() + "] ", LogLineFormat.Prefix(entry));
            }
        }

        [Fact]
        public void NullEntry_ThrowsRatherThanRenderingAGhostRow()
        {
            Assert.Throws<ArgumentNullException>(() => LogLineFormat.Prefix(null));
            Assert.Throws<ArgumentNullException>(() => LogLineFormat.Message(null));
            Assert.Throws<ArgumentNullException>(() => LogLineFormat.Line(null));
        }

        [Fact]
        public void MultiLineMessage_CollapsesToOneLine()
        {
            // A log row is a fixed-height Panel that clips what it cannot
            // draw, and MeasureString reports a multi-line string's WIDEST
            // LINE - so without this, a message whose first line happened to
            // fit rendered as that line alone, with no ellipsis and no
            // tooltip to say the rest had been dropped.
            Assert.Equal(
                "line one line two line three",
                LogLineFormat.Message(Entry("plan", "line one\r\nline two\tline three\n")));
        }

        [Fact]
        public void LeadingBreaks_AreDroppedRatherThanIndentingTheMessage()
        {
            Assert.Equal("boom", LogLineFormat.Message(Entry("plan", "\r\n\tboom")));
        }

        [Fact]
        public void SingleLineMessage_IsReturnedUnchanged()
        {
            // No allocation on the common path, and the row keeps one stable
            // string instance to re-ellipsize from across resizes.
            const string message = "solver ran in 12ms";
            Assert.Same(message, LogLineFormat.Message(Entry("plan", message)));
        }

        [Fact]
        public void Line_OfAMultiLineMessage_IsStillASingleLine()
        {
            // Copy joins these with Environment.NewLine and the tooltip shows
            // one of them, so an embedded break here would emit a ragged
            // multi-line block for a single entry.
            string line = LogLineFormat.Line(Entry("plan", "failed:\nSystem.Net.Http.HttpRequestException"));
            Assert.Equal("[WARN] 2026-08-16 14:03:09 [plan] failed: System.Net.Http.HttpRequestException", line);
            Assert.DoesNotContain("\n", line);
        }

        [Fact]
        public void Compose_ToleratesNullHalves()
        {
            Assert.Equal(" ", LogLineFormat.Compose(null, null));
            Assert.Equal("a ", LogLineFormat.Compose("a", null));
            Assert.Equal(" b", LogLineFormat.Compose(null, "b"));
        }
    }
}
