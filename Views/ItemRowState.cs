using Blish_HUD.Controls;

namespace TaimisToolbench.Views
{
    /// <summary>
    /// One row of the multi-item input strip (gw2efficiency
    /// parity): the plain session-persistent selection fields survive
    /// across Build() calls (tab switches) exactly like
    /// TreeSectionController's own override/ignore state - the live Blish
    /// controls do not (they are disposed and recreated by every
    /// Build()/ItemInputRowStrip.Rebuild call).
    /// </summary>
    internal sealed class ItemRowState
    {
        internal int? ItemId;
        internal string ItemName;

        // What the search box last read, kept whether or not it
        // resolved to an item. ItemName alone cannot carry this: it is
        // dropped the moment the text stops describing the picked item,
        // so seeding a rebuilt row from it would wipe half-typed text
        // on every row add/remove.
        internal string TypedText;
        internal string QuantityText = "1";

        internal Panel RowPanel;
        internal AutocompleteTextBox SearchBox;
        internal SuggestionPanel SuggestionPanel;
        internal TextBox QtyInput;
    }
}
