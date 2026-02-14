using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class ResolveResult
    {
        public int ItemsChecked { get; set; }
        public int OffersAdded { get; set; }
        public List<int> FailedItemIds { get; set; } = new List<int>();
    }
}
