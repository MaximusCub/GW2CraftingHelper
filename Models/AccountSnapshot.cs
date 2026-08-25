using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class AccountSnapshot
    {
        public DateTime CapturedAt { get; set; }

        public int CoinCopper { get; set; }

        public List<SnapshotItemEntry> Items { get; set; } = new List<SnapshotItemEntry>();

        public List<SnapshotWalletEntry> Wallet { get; set; } = new List<SnapshotWalletEntry>();

        // Per-character learned crafting disciplines. Deliberately NOT
        // defaulted to an empty list like Items/Wallet: null means "no
        // discipline data was ever captured" (old snapshot.json, degraded
        // fetch), distinct from "captured and empty". Consumers rely on
        // the distinction to never fabricate a "not trained" claim for a
        // snapshot that never looked.
        public List<SnapshotCharacterDiscipline> CharacterDisciplines { get; set; }
    }
}
