using System.Collections.Generic;
using System.Text;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Turns the places holding one item into the single line the Snapshot
    /// tab prints under that item's name. Blish-free and pure, so a test can
    /// drive the wording directly.
    /// <para>
    /// Counts are printed only when the reader cannot work the distribution
    /// out from the line itself. The row above the line already carries the
    /// account-wide total, so when every named place holds exactly one, the
    /// counts repeat what is on screen and are dropped. As soon as one place
    /// holds more than one, every place prints its count, including the
    /// places holding one.
    /// </para>
    /// </summary>
    internal static class SnapshotHoldLine
    {
        /// <summary>Separates two categories on the line.</summary>
        private const string CategorySeparator = "  ";

        /// <summary>
        /// Reads a raw AccountItemIndex source key as a place. An
        /// unrecognized key becomes
        /// <see cref="SnapshotHoldCategory.Unknown"/> and keeps its raw text,
        /// so real inventory the module does not yet know about still shows
        /// (KNOWN-ISSUES #31: never silently mask data).
        /// </summary>
        public static SnapshotHoldLocation FromSource(string rawSource, int count)
        {
            var location = new SnapshotHoldLocation
            {
                Count = count,
                RawSource = rawSource ?? "",
            };

            if (AccountItemIndex.TryGetCharacterName(rawSource, out string characterName))
            {
                location.Category = AccountItemIndex.IsEquipmentSource(rawSource)
                    ? SnapshotHoldCategory.Equipped
                    : SnapshotHoldCategory.Bags;
                location.CharacterName = characterName;
                return location;
            }

            switch (rawSource)
            {
                case AccountItemIndex.SourceSharedInventory:
                    location.Category = SnapshotHoldCategory.SharedInventory;
                    break;
                case AccountItemIndex.SourceBank:
                    location.Category = SnapshotHoldCategory.Bank;
                    break;
                case AccountItemIndex.SourceMaterialStorage:
                    location.Category = SnapshotHoldCategory.MaterialStorage;
                    break;
                case AccountItemIndex.SourceLegendaryArmory:
                    location.Category = SnapshotHoldCategory.LegendaryArmory;
                    break;
                default:
                    location.Category = SnapshotHoldCategory.Unknown;
                    break;
            }

            return location;
        }

        /// <summary>
        /// The whole line, or "" when nothing holds the item. Categories run
        /// in the order of <see cref="SnapshotHoldCategory"/>; characters run
        /// in the order the caller supplied, which is the order
        /// AccountItemIndex.GetPrioritizedSources put them in.
        /// </summary>
        public static string Format(IReadOnlyList<SnapshotHoldLocation> locations)
        {
            if (locations == null || locations.Count == 0)
            {
                return "";
            }

            // Indexed loops throughout, here and in AppendCategory: foreach
            // over IReadOnlyList boxes an enumerator, and a search rebuilds
            // every row on screen on every keystroke.
            bool showCounts = false;
            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location != null && location.Count != 1)
                {
                    showCounts = true;
                    break;
                }
            }

            var line = new StringBuilder();

            for (var category = SnapshotHoldCategory.SharedInventory;
                category <= SnapshotHoldCategory.LegendaryArmory;
                category++)
            {
                AppendCategory(line, locations, category, showCounts);
            }

            AppendUnrecognizedPlaces(line, locations, showCounts);

            return line.ToString();
        }

        /// <summary>
        /// Appends every unrecognized place, each under its own raw source
        /// key, in the order the caller supplied. They cannot share the
        /// category loop above: two unrecognized keys are two different
        /// places, and one label over both would hide one of them
        /// (KNOWN-ISSUES #31: never silently mask data).
        /// </summary>
        private static void AppendUnrecognizedPlaces(
            StringBuilder line,
            IReadOnlyList<SnapshotHoldLocation> locations,
            bool showCounts)
        {
            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location == null
                    || location.Category != SnapshotHoldCategory.Unknown)
                {
                    continue;
                }

                if (line.Length > 0)
                {
                    line.Append(CategorySeparator);
                }

                line.Append(CategoryLabel(location));

                if (showCounts)
                {
                    line.Append(": ").Append(location.Count);
                }
            }
        }

        /// <summary>
        /// Appends one category's places, or nothing when no place is in it.
        /// Scans the list twice rather than collecting the matches: this runs
        /// once per category per row.
        /// </summary>
        private static void AppendCategory(
            StringBuilder line,
            IReadOnlyList<SnapshotHoldLocation> locations,
            SnapshotHoldCategory category,
            bool showCounts)
        {
            SnapshotHoldLocation first = null;
            bool named = false;

            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location == null || location.Category != category)
                {
                    continue;
                }

                if (first == null)
                {
                    first = location;
                }

                named |= HasCharacterName(location);
            }

            if (first == null)
            {
                return;
            }

            if (line.Length > 0)
            {
                line.Append(CategorySeparator);
            }

            line.Append(CategoryLabel(first));

            if (!showCounts && !named)
            {
                // An account-wide place with nothing to say past its own
                // name: "Bank", not "Bank: 1".
                return;
            }

            line.Append(": ");

            // With counts on, one space keeps each name beside its own
            // bracketed count. With counts off, a comma is the only thing
            // separating two bare names.
            string separator = showCounts ? " " : ", ";
            bool wrote = false;

            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location == null || location.Category != category)
                {
                    continue;
                }

                if (wrote)
                {
                    line.Append(separator);
                }

                AppendPlace(line, location, showCounts);
                wrote = true;
            }
        }

        private static void AppendPlace(
            StringBuilder line, SnapshotHoldLocation location, bool showCounts)
        {
            if (HasCharacterName(location))
            {
                line.Append(location.CharacterName);
                if (showCounts)
                {
                    line.Append(" (").Append(location.Count).Append(")");
                }

                return;
            }

            line.Append(location.Count);
        }

        private static bool HasCharacterName(SnapshotHoldLocation location)
        {
            return !string.IsNullOrEmpty(location.CharacterName);
        }

        private static string CategoryLabel(SnapshotHoldLocation location)
        {
            switch (location.Category)
            {
                case SnapshotHoldCategory.SharedInventory: return "Shared Inventory";
                case SnapshotHoldCategory.Bags: return "Bags";
                case SnapshotHoldCategory.Equipped: return "Equipped";
                case SnapshotHoldCategory.Bank: return "Bank";
                case SnapshotHoldCategory.MaterialStorage: return "Material Storage";
                case SnapshotHoldCategory.LegendaryArmory: return "Legendary Armory";
                default: return location.RawSource.Length > 0 ? location.RawSource : "Unknown";
            }
        }
    }
}
