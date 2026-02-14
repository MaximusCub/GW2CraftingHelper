namespace GW2CraftingHelper.Services
{
    public class WikiLookupOptions
    {
        public int MaxConcurrentRequests { get; set; } = 3;
        public int MinDelayBetweenRequestsMs { get; set; } = 250;
        public int JitterMs { get; set; } = 50;
        public int MaxRetries { get; set; } = 3;
    }
}
