using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The socketed components of one owned stack, resolved from item ids
    /// to the stat blocks a tooltip can actually draw.
    /// <para>
    /// An id the session has no stat block for is DROPPED rather than
    /// drawn as a placeholder: the tooltip would otherwise claim a socket
    /// holds something unnameable, and the block reappears on the next
    /// hover once the background top-up lands.
    /// </para>
    /// </summary>
    internal sealed class SocketedUpgradeView
    {
        private static readonly IReadOnlyList<ItemStatBlock> NoBlocks = new List<ItemStatBlock>();

        public static readonly SocketedUpgradeView None = new SocketedUpgradeView(NoBlocks, NoBlocks);

        private SocketedUpgradeView(
            IReadOnlyList<ItemStatBlock> infusions, IReadOnlyList<ItemStatBlock> upgrades)
        {
            Infusions = infusions ?? NoBlocks;
            Upgrades = upgrades ?? NoBlocks;
        }

        public IReadOnlyList<ItemStatBlock> Infusions { get; }

        public IReadOnlyList<ItemStatBlock> Upgrades { get; }

        public bool IsEmpty => Infusions.Count == 0 && Upgrades.Count == 0;

        public static SocketedUpgradeView Resolve(
            SocketedUpgradeIds ids, Func<int, ItemStatBlock> getStatBlock)
        {
            if (ids == null || ids.IsEmpty || getStatBlock == null)
            {
                return None;
            }

            var infusions = Lookup(ids.Infusions, getStatBlock);
            var upgrades = Lookup(ids.Upgrades, getStatBlock);
            return infusions.Count == 0 && upgrades.Count == 0
                ? None
                : new SocketedUpgradeView(infusions, upgrades);
        }

        private static List<ItemStatBlock> Lookup(
            IReadOnlyList<int> ids, Func<int, ItemStatBlock> getStatBlock)
        {
            var blocks = new List<ItemStatBlock>(ids.Count);
            foreach (int id in ids)
            {
                if (id <= 0)
                {
                    continue;
                }

                var block = getStatBlock(id);
                if (block != null)
                {
                    blocks.Add(block);
                }
            }

            return blocks;
        }
    }
}
