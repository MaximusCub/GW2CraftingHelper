namespace VendorOfferUpdater.Models
{
    public static class Gw2Constants
    {
        public const int CoinCurrencyId = 1;

        // M37 (KNOWN-ISSUES #24): the three Homestead Refinement output
        // materials. Mirrors Models/Gw2Constants.cs's identical constants
        // in the main app - kept as a separate copy here since this tool
        // does not reference the main app's assembly (see the existing
        // duplicated VendorOffer/VendorOfferHasher pattern in this project).
        public const int RefinedHomesteadFiberItemId = 102306;
        public const int RefinedHomesteadMetalItemId = 102205;
        public const int RefinedHomesteadWoodItemId = 103049;

        public static bool IsHomesteadRefinementMaterialId(int itemId)
        {
            return itemId == RefinedHomesteadFiberItemId ||
                   itemId == RefinedHomesteadMetalItemId ||
                   itemId == RefinedHomesteadWoodItemId;
        }
    }
}
