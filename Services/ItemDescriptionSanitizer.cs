using System.Collections.Generic;
using System.Text;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Turns a /v2/items "description" into tooltip spans. The API's own
    /// markup vocabulary is small and closed: colour spans
    /// (<c>&lt;c=@flavor&gt;</c>, <c>&lt;c=@abilitytype&gt;</c>) with their
    /// <c>&lt;/c&gt;</c> closers, and <c>&lt;br&gt;</c> breaks alongside
    /// real newlines.
    ///
    /// <para>
    /// The colour spans carry MEANING, so they survive as
    /// <see cref="TooltipSpanRole"/>s rather than being discarded: the game
    /// colours only the marked runs (flavour teal, abilitytype pale yellow,
    /// warning red) and leaves unmarked description text white, which is
    /// the only way "A gift bag!" can be told apart from the quoted flavour
    /// that follows it inside one description string (spec section 1.4,
    /// gap G7).
    /// </para>
    ///
    /// <para>
    /// Anything ELSE in angle brackets is passed through verbatim rather
    /// than stripped. A blanket tag-stripper would silently delete real
    /// item text the day the API uses a bracket for something that is not
    /// markup; leaving an unknown tag visible is a reportable bug, deleting
    /// unknown text is a silent one.
    /// </para>
    /// </summary>
    public static class ItemDescriptionSanitizer
    {
        private static readonly IReadOnlyList<TooltipSpan> NoSpans = new List<TooltipSpan>();

        public static string Sanitize(string description)
        {
            var spans = SanitizeToSpans(description);
            if (spans.Count == 0)
            {
                return "";
            }
            var sb = new StringBuilder();
            foreach (var span in spans)
            {
                sb.Append(span.Text);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The same walk as <see cref="Sanitize"/>, keeping each run's
        /// colour role. Adjacent runs of one role are not merged - the
        /// builder concatenates them onto one line anyway - and the whole
        /// result is trimmed exactly as the plain form is, so
        /// <see cref="Sanitize"/> stays a concatenation of these spans.
        /// </summary>
        public static IReadOnlyList<TooltipSpan> SanitizeToSpans(string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return NoSpans;
            }

            var spans = new List<TooltipSpan>();
            // A stack, not a single current role: the API nests a
            // <c=@reminder> inside a <c=@flavor> on a handful of items, and
            // a closer must restore what was open rather than reset to
            // white.
            var openRoles = new Stack<TooltipSpanRole>();
            var sb = new StringBuilder(description.Length);
            var role = TooltipSpanRole.Default;

            void Flush()
            {
                if (sb.Length > 0)
                {
                    spans.Add(TooltipSpan.Styled(sb.ToString(), role));
                    sb.Clear();
                }
            }

            int i = 0;
            while (i < description.Length)
            {
                char c = description[i];

                if (c == '\r')
                {
                    // "\r\n" and a bare "\r" both collapse to one break,
                    // matching TooltipContentBuilder.Text's own rule.
                    sb.Append('\n');
                    i += (i + 1 < description.Length && description[i + 1] == '\n') ? 2 : 1;
                    continue;
                }

                if (c != '<')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                int close = description.IndexOf('>', i + 1);
                if (close < 0)
                {
                    // An unterminated '<' is ordinary text, not markup.
                    sb.Append(description, i, description.Length - i);
                    break;
                }

                string tag = description.Substring(i + 1, close - i - 1);
                if (IsBreakTag(tag))
                {
                    sb.Append('\n');
                }
                else if (tag == "/c")
                {
                    Flush();
                    role = openRoles.Count > 0 ? openRoles.Pop() : TooltipSpanRole.Default;
                }
                else if (tag.StartsWith("c="))
                {
                    Flush();
                    openRoles.Push(role);
                    role = RoleForColorTag(tag);
                }
                else
                {
                    sb.Append(description, i, close - i + 1);
                }

                i = close + 1;
            }

            Flush();
            return TrimEnds(spans);
        }

        /// <summary>
        /// The API's <c>&lt;c=@name&gt;</c> vocabulary, mapped to roles.
        /// An unrecognised colour name keeps its text at
        /// <see cref="TooltipSpanRole.Default"/> - the pre-role behaviour -
        /// rather than inventing a colour for it.
        /// </summary>
        private static TooltipSpanRole RoleForColorTag(string tag)
        {
            string name = tag.Substring(2).Trim().TrimStart('@');
            switch (name.ToLowerInvariant())
            {
                case "flavor":
                case "flavour":
                    return TooltipSpanRole.Flavor;
                case "abilitytype":
                    return TooltipSpanRole.AbilityType;
                case "warning":
                    return TooltipSpanRole.Warning;
                // gw2efficiency renders reminder text at #afafaf, which is
                // this module's Muted grey to within two levels - no
                // separate role earns its keep for it.
                case "reminder":
                    return TooltipSpanRole.Muted;
                default:
                    return TooltipSpanRole.Default;
            }
        }

        // Trims the whole run the way the plain form always has, without
        // letting an all-whitespace edge span survive as an empty one.
        private static IReadOnlyList<TooltipSpan> TrimEnds(List<TooltipSpan> spans)
        {
            while (spans.Count > 0)
            {
                string trimmed = spans[0].Text.TrimStart();
                if (trimmed.Length > 0)
                {
                    spans[0] = spans[0].WithText(trimmed);
                    break;
                }
                spans.RemoveAt(0);
            }

            while (spans.Count > 0)
            {
                int last = spans.Count - 1;
                string trimmed = spans[last].Text.TrimEnd();
                if (trimmed.Length > 0)
                {
                    spans[last] = spans[last].WithText(trimmed);
                    break;
                }
                spans.RemoveAt(last);
            }

            return spans;
        }

        private static bool IsBreakTag(string tag)
        {
            string trimmed = tag.TrimEnd('/', ' ');
            return trimmed == "br";
        }
    }
}
