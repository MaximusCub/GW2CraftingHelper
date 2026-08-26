using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    internal class UsedMaterial
    {
        public int ItemId { get; set; }

        public int QuantityUsed { get; set; }

        /// <summary>
        /// Per-source breakdown of where this material was consumed from.
        /// Null when produced by the legacy (Dictionary pool) overload.
        /// Non-null (possibly empty) when produced by the sourced
        /// (AccountItemIndex) overload.
        /// </summary>
        public List<MaterialSourceAllocation> Sources { get; set; }
    }
}
