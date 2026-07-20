using Blish_HUD.Settings;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class ModuleSettings
    {
        public SettingEntry<int> ModalDialogX { get; private set; }
        public SettingEntry<int> ModalDialogY { get; private set; }

        // User-provided coin valuations for non-coin currencies (karma,
        // laurels, ...), stored as JSON (currencyId -> copper per unit).
        // SettingCollection has no built-in support for a CurrencyValuation
        // object, so it is persisted as a raw string; CurrencyValuationSerializer
        // (Blish-free) does the actual conversion so that logic is unit-testable.
        public SettingEntry<string> CurrencyValuationsJson { get; private set; }

        public ModuleSettings(SettingCollection settings)
        {
            ModalDialogX = settings.DefineSetting(
                "ModalDialogX", -1,
                () => "Modal Dialog X",
                () => "Horizontal position of the modal dialog");

            ModalDialogY = settings.DefineSetting(
                "ModalDialogY", -1,
                () => "Modal Dialog Y",
                () => "Vertical position of the modal dialog");

            CurrencyValuationsJson = settings.DefineSetting(
                "CurrencyValuationsJson", string.Empty,
                () => "Currency Valuations",
                () => "User-provided coin values for non-coin currencies (JSON)");
        }

        /// <summary>
        /// Reads the persisted currency valuations. Returns
        /// CurrencyValuation.None when nothing has been configured or the
        /// stored value cannot be parsed.
        /// </summary>
        public CurrencyValuation GetCurrencyValuation()
        {
            return CurrencyValuationSerializer.Deserialize(CurrencyValuationsJson.Value);
        }

        /// <summary>
        /// Persists the given currency valuations.
        /// </summary>
        public void SetCurrencyValuation(CurrencyValuation valuation)
        {
            CurrencyValuationsJson.Value = CurrencyValuationSerializer.Serialize(valuation);
        }

        public void ResetToDefaults()
        {
            ModalDialogX.Value = -1;
            ModalDialogY.Value = -1;
            CurrencyValuationsJson.Value = string.Empty;
        }
    }
}
