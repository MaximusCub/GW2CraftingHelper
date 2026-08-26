using System;
using System.Collections.Generic;
using System.Globalization;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// The Settings tab's save-gated control values, captured as a flat
    /// key/value bag so "has the user edited anything since the last load
    /// or save?" is a plain value comparison with no Blish types in it.
    /// A bag rather than a typed record because the tab's field set is
    /// open-ended - 47 currency amounts, 47 Ignore flags, three Homestead
    /// tiers, two logging fields and one snapshot field today - and every
    /// one of them compares the same way.
    ///
    /// Only fields the tab's single Save button persists belong here.
    /// The Diagnostics checkbox is deliberately absent: its CheckedChanged
    /// handler writes through to ModuleSettings immediately, so it is
    /// never an unsaved change and listing it would make every toggle of
    /// it raise a save prompt for a value already on disk.
    /// </summary>
    internal sealed class SettingsFormState
    {
        // Ordinal, and sorted, so ChangedKeys is deterministic regardless
        // of the order the caller captured the controls in.
        private readonly SortedDictionary<string, string> _fields =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        public int FieldCount => _fields.Count;

        /// <summary>
        /// Records one text field. Values are trimmed and null is stored
        /// as empty, so whitespace the user typed and then deleted - or a
        /// control that reports null rather than "" for an empty box -
        /// does not read as an edit.
        /// </summary>
        public void AddText(string key, string value)
        {
            AddField(key, value == null ? string.Empty : value.Trim());
        }

        /// <summary>
        /// Records one checkbox. Stored as text so a flag and a box share
        /// one comparison path.
        /// </summary>
        public void AddFlag(string key, bool value)
        {
            AddField(key, value ? "1" : "0");
        }

        /// <summary>
        /// Keys whose value differs between this state and
        /// <paramref name="baseline"/>, including keys present in only one
        /// of the two. A null baseline means nothing is known to compare
        /// against, which is reported as no changes rather than as every
        /// field changed - the tab has no baseline until it has been built
        /// once, and prompting to save a form nobody has seen is wrong.
        /// </summary>
        public IReadOnlyList<string> ChangedKeys(SettingsFormState baseline)
        {
            var changed = new List<string>();
            if (baseline == null)
            {
                return changed;
            }

            foreach (var field in _fields)
            {
                if (!baseline._fields.TryGetValue(field.Key, out string baselineValue)
                    || !string.Equals(field.Value, baselineValue, StringComparison.Ordinal))
                {
                    changed.Add(field.Key);
                }
            }

            foreach (var field in baseline._fields)
            {
                if (!_fields.ContainsKey(field.Key))
                {
                    changed.Add(field.Key);
                }
            }

            changed.Sort(StringComparer.Ordinal);
            return changed;
        }

        public bool IsDirtyAgainst(SettingsFormState baseline)
        {
            return ChangedKeys(baseline).Count > 0;
        }

        public static string CurrencyAmountKey(int currencyId)
        {
            return "currency." + currencyId.ToString(CultureInfo.InvariantCulture) + ".amount";
        }

        public static string CurrencyIgnoreKey(int currencyId)
        {
            return "currency." + currencyId.ToString(CultureInfo.InvariantCulture) + ".ignore";
        }

        public static string HomesteadTierKey(int materialItemId)
        {
            return "homestead." + materialItemId.ToString(CultureInfo.InvariantCulture) + ".tier";
        }

        public const string LogMaxSizeMbKey = "log.maxSizeMb";
        public const string LogRetentionDaysKey = "log.retentionDays";
        public const string SnapshotRefreshIntervalMinutesKey = "snapshot.refreshIntervalMinutes";

        // Duplicates are rejected rather than overwritten: two controls
        // sharing a key would collapse into one comparison and silently
        // stop reporting edits to whichever lost, which is exactly the
        // failure this type exists to prevent. Every production key is
        // built from an id that is unique by construction (the curated
        // currency list is a SortedSet, the Homestead rows are three
        // distinct item ids), so this can only fire on a wiring mistake.
        private void AddField(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Field key is required.", nameof(key));
            }

            if (_fields.ContainsKey(key))
            {
                throw new ArgumentException("Duplicate field key: " + key, nameof(key));
            }

            _fields[key] = value;
        }
    }
}
