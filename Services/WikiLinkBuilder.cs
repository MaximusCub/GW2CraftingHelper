using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure GW2 wiki URL construction (UI-bundle milestone, Feature A -
    /// wiki links). Blish-free by design so the title-encoding rules
    /// (spaces to underscores, percent-encoding for apostrophes and other
    /// reserved characters) can be exercised by a real unit test without
    /// any Control/Process dependency - the actual browser launch is a
    /// separate, untested, side-effecting call (see WikiLinkLauncher).
    /// <para>
    /// Mirrors standard MediaWiki title normalization: a space becomes an
    /// underscore, then the remaining title is percent-encoded via
    /// <see cref="Uri.EscapeDataString(string)"/> (RFC 3986 "unreserved"
    /// characters - letters, digits, '-', '.', '_', '~' - are left
    /// literal, so the underscores just inserted survive the encode step
    /// unchanged). The GW2 wiki's own recipe "sheet" pages use a literal
    /// namespace-style colon prefix (e.g. "Recipe:_Bolt_of_Damask") which
    /// this deliberately does NOT run through EscapeDataString (that would
    /// turn the colon into "%3A", which does not match the site's real
    /// URLs) - see BuildRecipeSheetUrl.
    /// </para>
    /// </summary>
    public static class WikiLinkBuilder
    {
        private const string BaseUrl = "https://wiki.guildwars2.com/wiki/";
        private const string RecipeNamespacePrefix = "Recipe:_";
        private const string AcquisitionAnchor = "#Acquisition";

        /// <summary>
        /// The item's own wiki page, e.g. "Zojja's Claymore" -&gt;
        /// ".../wiki/Zojja%27s_Claymore".
        /// </summary>
        public static string BuildItemPageUrl(string itemName)
        {
            string title = EncodeTitle(itemName);
            return title.Length == 0 ? null : BaseUrl + title;
        }

        /// <summary>
        /// The item's wiki page with a "#Acquisition" anchor appended -
        /// degrades gracefully to the page top on a wiki page that has no
        /// such section (page titles match item names via wiki redirects,
        /// per the feature spec).
        /// </summary>
        public static string BuildItemAcquisitionUrl(string itemName)
        {
            string url = BuildItemPageUrl(itemName);
            return url == null ? null : url + AcquisitionAnchor;
        }

        /// <summary>
        /// The recipe's own "Recipe: &lt;output item name&gt;" sheet page -
        /// only meaningful for a recipe unlocked via LearnedFromItem (a
        /// consumable recipe sheet), which is the only kind of recipe that
        /// has one.
        /// </summary>
        public static string BuildRecipeSheetUrl(string outputItemName)
        {
            string title = EncodeTitle(outputItemName);
            return title.Length == 0 ? null : BaseUrl + RecipeNamespacePrefix + title;
        }

        /// <summary>
        /// Required Recipes Missing! row link target (flag-based per the
        /// feature spec): a recipe unlocked via a LearnedFromItem
        /// consumable links to its own "Recipe: &lt;name&gt;" sheet page;
        /// every other recipe links to the output item's own page with an
        /// "#Acquisition" anchor.
        /// </summary>
        public static string BuildRequiredRecipeUrl(string outputItemName, bool isLearnedFromItem)
        {
            return isLearnedFromItem
                ? BuildRecipeSheetUrl(outputItemName)
                : BuildItemAcquisitionUrl(outputItemName);
        }

        private static string EncodeTitle(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }

            string underscored = name.Trim().Replace(' ', '_');
            return Uri.EscapeDataString(underscored);
        }
    }
}
