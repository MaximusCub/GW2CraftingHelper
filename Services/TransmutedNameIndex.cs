using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The skin names a Snapshot row may show or match, keyed by item id.
    /// <para>
    /// The game applies a skin per copy, and a Snapshot row is one item id
    /// summed over every copy of it. So a row takes a skin's name as its
    /// own only when every copy wears that same skin; when they differ it
    /// keeps the item's name, because a name only one copy wears would
    /// describe items the row also covers. Search is not held to that
    /// rule - it matches any skin any copy wears, so the name the game
    /// shows always finds the item.
    /// </para>
    /// <para>
    /// A skin whose name equals the item's own name is not a
    /// transmutation and is read as no skin at all.
    /// </para>
    /// <para>Blish-free, so the whole rule is unit-testable (repo
    /// invariant), same precedent as SocketedUpgradeIndex.</para>
    /// </summary>
    internal static class TransmutedNameIndex
    {
        private static readonly Dictionary<int, TransmutedItemNames> NoSkins =
            new Dictionary<int, TransmutedItemNames>();

        public static IReadOnlyDictionary<int, TransmutedItemNames> Build(
            IReadOnlyList<SnapshotItemEntry> items)
        {
            if (items == null || items.Count == 0)
            {
                return NoSkins;
            }

            // Ids first, states second: this walks every row of a
            // snapshot, thousands of them on a full account, and only the
            // handful wearing a skin need a state object at all.
            var skinned = new HashSet<int>();
            foreach (var entry in items)
            {
                if (entry != null && entry.ItemId > 0 && SkinNameOf(entry).Length > 0)
                {
                    skinned.Add(entry.ItemId);
                }
            }

            if (skinned.Count == 0)
            {
                return NoSkins;
            }

            var states = new Dictionary<int, State>(skinned.Count);

            foreach (var entry in items)
            {
                if (entry == null || !skinned.Contains(entry.ItemId))
                {
                    continue;
                }

                string skin = SkinNameOf(entry);
                if (!states.TryGetValue(entry.ItemId, out var state))
                {
                    state = new State { Agreed = skin, CopiesAgree = true };
                    states[entry.ItemId] = state;
                }
                else if (state.CopiesAgree && !string.Equals(state.Agreed, skin, StringComparison.Ordinal))
                {
                    // One bare copy and one skinned copy disagree as much
                    // as two differently skinned ones do: "no skin" is an
                    // answer, not a missing reading.
                    state.CopiesAgree = false;
                    state.Agreed = "";
                }

                if (skin.Length > 0 && !state.Names.Contains(skin))
                {
                    state.Names.Add(skin);
                }
            }

            var result = new Dictionary<int, TransmutedItemNames>(states.Count);
            foreach (var pair in states)
            {
                result[pair.Key] = new TransmutedItemNames(pair.Value.Agreed, pair.Value.Names);
            }

            return result;
        }

        private static string SkinNameOf(SnapshotItemEntry entry)
        {
            string skin = entry.SkinName ?? "";
            if (skin.Length == 0)
            {
                return "";
            }

            return string.Equals(skin, entry.Name ?? "", StringComparison.Ordinal) ? "" : skin;
        }

        private sealed class State
        {
            public string Agreed = "";

            public bool CopiesAgree;

            public readonly List<string> Names = new List<string>();
        }
    }
}
