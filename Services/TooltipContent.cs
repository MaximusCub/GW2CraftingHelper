using System.Collections.Generic;
using System.Text;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// A tooltip's content as structure rather than as one flat string: a
    /// list of lines, each a run of spans that are either prose or a coin
    /// amount. The coin span is the whole reason this type exists - a coin
    /// amount rendered as "1g 23s 45c" text is the audit H6 complaint, and
    /// only a span that still knows its copper value can be drawn with the
    /// gold/silver/copper icons (icons RIGHT of their numbers, repo
    /// invariant) by the rich tooltip surface.
    ///
    /// Every span carries plain text too, so <see cref="ToPlainText"/>
    /// reproduces byte-for-byte what the composers used to return. That is
    /// what lets each composer keep ONE implementation while still serving
    /// the plain <c>BasicTooltipText</c> path and its existing tests.
    /// </summary>
    public sealed class TooltipContent
    {
        public static readonly TooltipContent Empty = new TooltipContent(new List<TooltipLine>());

        private readonly IReadOnlyList<TooltipLine> _lines;

        internal TooltipContent(IReadOnlyList<TooltipLine> lines)
        {
            _lines = lines ?? new List<TooltipLine>();
        }

        public IReadOnlyList<TooltipLine> Lines => _lines;

        public bool IsEmpty => _lines.Count == 0;

        /// <summary>
        /// Wraps a finished plain string (the shape most call sites still
        /// have) into single-text-span lines. Hard breaks become line
        /// boundaries; nothing is re-wrapped here.
        /// </summary>
        public static TooltipContent FromText(string text)
        {
            var builder = new TooltipContentBuilder();
            builder.Text(text);
            return builder.Build();
        }

        /// <summary>
        /// For a composer that assembles its lines as a list it still needs
        /// to reorder (<c>TreeRowTooltipComposer</c> inserts the caption at
        /// the front after the fact) rather than streaming them into a
        /// <see cref="TooltipContentBuilder"/>.
        /// </summary>
        public static TooltipContent FromLines(IReadOnlyList<TooltipLine> lines)
        {
            return lines == null || lines.Count == 0 ? Empty : new TooltipContent(lines);
        }

        public static TooltipLine TextLine(string text)
        {
            return new TooltipLine(new List<TooltipSpan> { TooltipSpan.FromText(text ?? "") });
        }

        public static TooltipLine Line(params TooltipSpan[] spans)
        {
            return new TooltipLine(spans ?? new TooltipSpan[0]);
        }

        /// <summary>
        /// The exact string the plain path assigns to
        /// <c>BasicTooltipText</c>. Coin spans render their own plain text,
        /// which is why the two composers' deliberately different coin
        /// formats (always-three-units vs leading-units-omitted) survive
        /// the round trip unchanged.
        /// </summary>
        public string ToPlainText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _lines.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }
                _lines[i].AppendPlainText(sb);
            }
            return sb.ToString();
        }

        /// <summary>
        /// One plain string per line - the shape
        /// <c>TreeRowTooltipComposer</c>'s callers already pass around.
        /// </summary>
        public List<string> ToPlainLines()
        {
            var lines = new List<string>(_lines.Count);
            foreach (var line in _lines)
            {
                var sb = new StringBuilder();
                line.AppendPlainText(sb);
                lines.Add(sb.ToString());
            }
            return lines;
        }
    }

    public sealed class TooltipLine
    {
        private readonly IReadOnlyList<TooltipSpan> _spans;

        internal TooltipLine(IReadOnlyList<TooltipSpan> spans)
        {
            _spans = spans ?? new List<TooltipSpan>();
        }

        public IReadOnlyList<TooltipSpan> Spans => _spans;

        internal void AppendPlainText(StringBuilder sb)
        {
            foreach (var span in _spans)
            {
                sb.Append(span.Text);
            }
        }
    }

    /// <summary>
    /// Prose, or a coin amount that still knows its copper value.
    /// <see cref="Text"/> is populated in both cases: for a coin span it is
    /// the caller's own plain rendering, used by the plain tooltip path and
    /// as the width fallback nowhere else.
    /// </summary>
    public readonly struct TooltipSpan
    {
        private TooltipSpan(string text, long coinCopper, bool isCoin)
        {
            Text = text ?? "";
            CoinCopper = coinCopper;
            IsCoin = isCoin;
        }

        public string Text { get; }

        public long CoinCopper { get; }

        public bool IsCoin { get; }

        public static TooltipSpan FromText(string text)
        {
            return new TooltipSpan(text, 0, false);
        }

        public static TooltipSpan FromCoin(long copper, string plainText)
        {
            return new TooltipSpan(plainText, copper, true);
        }
    }

    /// <summary>
    /// Accumulates spans into lines. Composers build with this; the
    /// tooltip facility composes several composers' results with
    /// <see cref="Append"/> and <see cref="Separator"/> instead of the
    /// "\n\n" string concatenation the pill tooltip used to do.
    /// </summary>
    public sealed class TooltipContentBuilder
    {
        private readonly List<TooltipLine> _lines = new List<TooltipLine>();
        private List<TooltipSpan> _current;

        public bool IsEmpty => _lines.Count == 0 && _current == null;

        /// <summary>
        /// Appends prose to the current line. An embedded hard break ends
        /// the line, so a composer can keep handing over the multi-line
        /// strings it already builds.
        /// </summary>
        public TooltipContentBuilder Text(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return this;
            }

            string normalized = text.IndexOf('\r') >= 0 ? text.Replace("\r\n", "\n").Replace('\r', '\n') : text;
            int start = 0;
            while (true)
            {
                int brk = normalized.IndexOf('\n', start);
                string piece = brk < 0 ? normalized.Substring(start) : normalized.Substring(start, brk - start);
                if (piece.Length > 0)
                {
                    Current().Add(TooltipSpan.FromText(piece));
                }
                if (brk < 0)
                {
                    return this;
                }
                EndLine();
                start = brk + 1;
            }
        }

        public TooltipContentBuilder Coin(long copper, string plainText)
        {
            Current().Add(TooltipSpan.FromCoin(copper, plainText));
            return this;
        }

        /// <summary>
        /// Commits the current line, blank line included - a builder with a
        /// started-but-empty line still owes that blank row, which is how
        /// the composers' deliberate separator lines survive.
        /// </summary>
        public TooltipContentBuilder EndLine()
        {
            _lines.Add(new TooltipLine(_current ?? new List<TooltipSpan>()));
            _current = null;
            return this;
        }

        /// <summary>
        /// The blank line between two composed blocks - the structural
        /// replacement for the "\n\n" the pill tooltip concatenated. A no-op
        /// on an empty builder, so a block that turns out to be first never
        /// opens with a stray blank row.
        /// </summary>
        public TooltipContentBuilder Separator()
        {
            if (IsEmpty)
            {
                return this;
            }
            if (_current != null)
            {
                EndLine();
            }
            _lines.Add(new TooltipLine(new List<TooltipSpan>()));
            return this;
        }

        public TooltipContentBuilder Append(TooltipContent other)
        {
            if (other == null || other.IsEmpty)
            {
                return this;
            }
            if (_current != null)
            {
                EndLine();
            }
            _lines.AddRange(other.Lines);
            return this;
        }

        public TooltipContent Build()
        {
            if (_current != null)
            {
                EndLine();
            }
            return _lines.Count == 0 ? TooltipContent.Empty : new TooltipContent(_lines);
        }

        private List<TooltipSpan> Current()
        {
            return _current ?? (_current = new List<TooltipSpan>());
        }
    }
}
