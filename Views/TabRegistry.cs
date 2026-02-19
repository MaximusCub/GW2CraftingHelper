using System.Collections.Generic;

namespace GW2CraftingHelper.Views
{
    public static class TabRegistry
    {
        public const int TabSnapshot = 0;
        public const int TabCraftingPlan = 1;
        public const int TabLog = 2;
        public const int TabPlanHistory = 3;
        public const int TabCraftingRanker = 4;
        public const int TabSettings = 5;
        public const int TabAbout = 6;

        public static IReadOnlyList<TabDefinition> Tabs { get; } = new[]
        {
            new TabDefinition("Snapshot"),
            new TabDefinition("Crafting Plan"),
            new TabDefinition("Log"),
            new TabDefinition("Plan History", isPlaceholder: true),
            new TabDefinition("Crafting Ranker", isPlaceholder: true),
            new TabDefinition("Settings", isPlaceholder: true),
            new TabDefinition("About", isPlaceholder: true),
        };
    }
}
