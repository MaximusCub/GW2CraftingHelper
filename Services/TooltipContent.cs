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
    /// What a span MEANS, not what colour it is. The rich surface resolves
    /// a role to a colour (<c>RichTooltipSurface.RenderRow</c>); this file
    /// - and every composer that builds content - stays XNA-free, which is
    /// what keeps composer tests Blish-free (repo invariant). Only
    /// <c>Views/Rendering/RarityColors</c> knows a
    /// <c>Microsoft.Xna.Framework.Color</c>.
    /// </summary>
    public enum TooltipSpanRole
    {
        /// <summary>Ordinary tooltip prose.</summary>
        Default,

        /// <summary>An item name, coloured by the rarity carried on the
        /// span itself (<see cref="TooltipSpan.RarityKey"/>).</summary>
        Rarity,

        /// <summary>An upgrade's granted bonus - a rune bonus line, a sigil
        /// or infusion buff, a food nourishment line.</summary>
        Bonus,

        /// <summary>
        /// A bonus tier the wearer has not reached. Reserved and unused:
        /// greying a tier needs the character's equipped count, which is
        /// instance state /v2/items cannot carry (spec section 3.2). It
        /// exists so an equipped-aware surface does not have to re-plumb
        /// the role through every composer to get it.
        /// </summary>
        BonusInactive,

        /// <summary>The item's <c>&lt;c=@flavor&gt;</c> prose.</summary>
        Flavor,

        /// <summary>The item's <c>&lt;c=@abilitytype&gt;</c> lead-in
        /// ("Element: ").</summary>
        AbilityType,

        /// <summary>The item's <c>&lt;c=@warning&gt;</c> run.</summary>
        Warning,

        /// <summary>
        /// A genuine secondary annotation - the game's own grey, e.g.
        /// "0/500 in Material Storage". NOT the identity block, which the
        /// game renders white (spec section 1.4, gap G4).
        /// </summary>
        Muted
    }

    /// <summary>
    /// Prose, or a coin amount that still knows its copper value.
    /// <see cref="Text"/> is populated in both cases: for a coin span it is
    /// the caller's own plain rendering, used by the plain tooltip path and
    /// as the width fallback nowhere else.
    /// </summary>
    public readonly struct TooltipSpan
    {
        private TooltipSpan(string text, long coinCopper, bool isCoin, TooltipSpanRole role, string rarityKey)
        {
            Text = text ?? "";
            CoinCopper = coinCopper;
            IsCoin = isCoin;
            Role = role;
            RarityKey = rarityKey;
        }

        public string Text { get; }

        public long CoinCopper { get; }

        public bool IsCoin { get; }

        public TooltipSpanRole Role { get; }

        /// <summary>
        /// GW2 API rarity string, meaningful only when
        /// <see cref="Role"/> is <see cref="TooltipSpanRole.Rarity"/>. A
        /// rarity STRING rather than a colour, for the reason on
        /// <see cref="TooltipSpanRole"/>; null/unknown renders the same
        /// neutral grey RarityColors already falls back to.
        /// </summary>
        public string RarityKey { get; }

        public static TooltipSpan FromText(string text)
        {
            return new TooltipSpan(text, 0, false, TooltipSpanRole.Default, null);
        }

        public static TooltipSpan Styled(string text, TooltipSpanRole role)
        {
            return new TooltipSpan(text, 0, false, role, null);
        }

        public static TooltipSpan RarityText(string text, string rarity)
        {
            return new TooltipSpan(text, 0, false, TooltipSpanRole.Rarity, rarity);
        }

        public static TooltipSpan FromCoin(long copper, string plainText)
        {
            return new TooltipSpan(plainText, copper, true, TooltipSpanRole.Default, null);
        }

        /// <summary>
        /// The same span carrying different text - how the wrapper splits a
        /// prose span into rows without losing its role. Re-creating the
        /// piece with <see cref="FromText"/> instead would silently reset
        /// every wrapped line to <see cref="TooltipSpanRole.Default"/>,
        /// i.e. a long item name would lose its rarity colour the moment it
        /// wrapped.
        /// </summary>
        internal TooltipSpan WithText(string text)
        {
            return new TooltipSpan(text, CoinCopper, IsCoin, Role, RarityKey);
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
            return AppendText(text, TooltipSpan.FromText(""));
        }

        /// <summary>Prose that means something (see <see cref="TooltipSpanRole"/>).</summary>
        public TooltipContentBuilder Styled(string text, TooltipSpanRole role)
        {
            return AppendText(text, TooltipSpan.Styled("", role));
        }

        /// <summary>An item name coloured by its GW2 rarity string.</summary>
        public TooltipContentBuilder RarityText(string text, string rarity)
        {
            return AppendText(text, TooltipSpan.RarityText("", rarity));
        }

        // template carries the role/rarity every piece of this text
        // inherits; only its text differs per hard break.
        private TooltipContentBuilder AppendText(string text, TooltipSpan template)
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
                    Current().Add(template.WithText(piece));
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
