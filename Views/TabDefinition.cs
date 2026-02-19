namespace GW2CraftingHelper.Views
{
    public class TabDefinition
    {
        public string Name { get; }
        public bool IsPlaceholder { get; }

        public TabDefinition(string name, bool isPlaceholder = false)
        {
            Name = name;
            IsPlaceholder = isPlaceholder;
        }
    }
}
