using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class AccountSnapshot
    {
        public DateTime                CapturedAt { get; set; }
        public int                     CoinCopper { get; set; }
        public List<SnapshotItemEntry>   Items    { get; set; } = new List<SnapshotItemEntry>();
        public List<SnapshotWalletEntry> Wallet   { get; set; } = new List<SnapshotWalletEntry>();

        // W3C (per-character discipline display, gw2efficiency parity):
        // per-character learned crafting disciplines captured during a
        // Refresh Now fetch (see Gw2AccountSnapshotService's per-character
        // loop). Deliberately NOT defaulted to an empty list like
        // Items/Wallet above - null is a distinct, meaningful state ("no
        // discipline data was ever captured for this snapshot": a pre-W3C
        // snapshot.json loaded from disk, via Newtonsoft leaving a missing
        // JSON field at its C# default) from "captured, and it happens to
        // be empty" (e.g. an account with zero characters).
        // PlanViewModelBuilder.BuildDisciplinesSection relies on exactly
        // this distinction to never fabricate a "not trained on any
        // character" claim for a snapshot that never actually looked.
        public List<SnapshotCharacterDiscipline> CharacterDisciplines { get; set; }
    }
}
