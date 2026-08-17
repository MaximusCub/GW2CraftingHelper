using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Which Trading Post price is used to cost material acquisition.
    /// </summary>
    // Review-fix: serialized as its enum
    // NAME rather than Newtonsoft's bare-int default - PersistedPlan.
    // PriceBasis is the only member of this enum's type ever written to
    // disk (see Models/PersistedPlan.cs), so this is a type-level attribute
    // with no other consumer to disturb, matching the same
    // StringEnumConverter precedent Services/ModuleLogEntry.cs already uses
    // for ModuleLogLevel. A future member reorder can no longer silently
    // remap an already-persisted plan's price basis to a different value.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PriceBasis
    {
        /// <summary>
        /// Buy instantly from the lowest sell listing (sells.unit_price).
        /// Immediate but more expensive.
        /// </summary>
        InstantBuy = 0,

        /// <summary>
        /// Place buy orders at the highest current buy order
        /// (buys.unit_price). Cheaper but not instant.
        /// </summary>
        BuyOrder = 1
    }
}
