namespace TaimisToolbench.Models
{
    /// <summary>
    /// One resolved attribute row of an item's stat block - "+141 Power".
    /// Carries a DISPLAY name, never an API attribute token and never an
    /// itemstats id (repo invariant: ids are internal-only).
    /// </summary>
    internal sealed class ItemAttributeLine
    {
        public ItemAttributeLine(string displayName, int value)
        {
            DisplayName = displayName ?? "";
            Value = value;
        }

        public string DisplayName { get; }

        public int Value { get; }
    }
}
