using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models {

    public class AccountSnapshot {
        public DateTime                CapturedAt { get; set; }
        public int                     CoinCopper { get; set; }
        public List<SnapshotItemEntry>   Items    { get; set; } = new List<SnapshotItemEntry>();
        public List<SnapshotWalletEntry> Wallet   { get; set; } = new List<SnapshotWalletEntry>();
    }

}
