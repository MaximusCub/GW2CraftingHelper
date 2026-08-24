using System.Text;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Turns a /v2/items "description" into plain text. The API's own
    /// markup vocabulary is small and closed: colour spans
    /// (<c>&lt;c=@flavor&gt;</c>, <c>&lt;c=@abilitytype&gt;</c>) with their
    /// <c>&lt;/c&gt;</c> closers, and <c>&lt;br&gt;</c> breaks alongside
    /// real newlines.
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
        public static string Sanitize(string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return "";
            }

            var sb = new StringBuilder(description.Length);
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

                // A colour span is dropped, not honoured: the flavour block
                // already renders in its own muted role, and a tooltip span
                // carries a semantic role rather than an arbitrary colour.
                string tag = description.Substring(i + 1, close - i - 1);
                if (IsBreakTag(tag))
                {
                    sb.Append('\n');
                }
                else if (!IsColorTag(tag))
                {
                    sb.Append(description, i, close - i + 1);
                }

                i = close + 1;
            }

            return sb.ToString().Trim();
        }

        private static bool IsColorTag(string tag)
        {
            return tag == "/c" || tag.StartsWith("c=");
        }

        private static bool IsBreakTag(string tag)
        {
            string trimmed = tag.TrimEnd('/', ' ');
            return trimmed == "br";
        }
    }
}
