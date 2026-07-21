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

        // gw2efficiency-style "value own materials" (M28; M34-B2a #3
        // upgraded this from a display-only opportunity-cost tweak into a
        // real force-buy pre-pass - see OwnedMaterialsForceBuyPrePass):
        // when enabled, a node is force-excluded from crafting whenever
        // buying it outright costs less than 85% of its own components'
        // fresh buy cost (gw2efficiency's getCheaperToBuyItemIds), and the
        // plan's profit figure is reduced by owned materials' sell
        // opportunity cost. Default TRUE to match gw2efficiency's own
        // "valueOwnItems" default (m34-r2-gw2e-owned-materials.md Section
        // 3.1: valueOwnItems defaults true whenever the separate "use own
        // materials" master toggle - CraftingPlanView's own checkbox,
        // default off - is on). This setting's force-buy effect only
        // applies when an account snapshot is actually driving reduction
        // (CraftingPlanPipeline's own, deliberately narrower gate - see its
        // Step 6.5 comment) - with no snapshot it stays fully inert, same
        // as the profit-display opportunity-cost figure it also drives.
        // Now surfaced as a checkbox in the Settings tab (see
        // SettingsTabContent) - previously flip-only-via-JSON like
        // ScrollDiagnosticsEnabled below.
        public SettingEntry<bool> ValueOwnMaterials { get; private set; }

        // M33 C1 (#12 diagnostics): gates the scroll-machinery diagnostic
        // logging in CraftingPlanView (wheel events, restore/guard writes
        // and state transitions). Default false; instrumentation only -
        // never changes scroll/guard/restore behavior. Unlike
        // ValueOwnMaterials (see above), this has no checkbox in the
        // Settings tab; it is flipped via the persisted settings JSON for
        // diagnosis.
        public SettingEntry<bool> ScrollDiagnosticsEnabled { get; private set; }

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

            ValueOwnMaterials = settings.DefineSetting(
                "ValueOwnMaterials", true,
                () => "Value own materials",
                () => "Force-buy items where buying beats crafting from fresh components by more than 15%, and value owned materials at their sell opportunity cost instead of treating them as free");

            ScrollDiagnosticsEnabled = settings.DefineSetting(
                "ScrollDiagnosticsEnabled", false,
                () => "Scroll diagnostics",
                () => "Log scroll machinery events for debugging");
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

        /// <summary>
        /// Maps the ValueOwnMaterials toggle onto the pipeline's
        /// OwnMaterialsMode enum. Defaults to Valued (see ValueOwnMaterials'
        /// own doc comment for why).
        /// </summary>
        public OwnMaterialsMode GetOwnMaterialsMode()
        {
            return ValueOwnMaterials.Value ? OwnMaterialsMode.Valued : OwnMaterialsMode.Free;
        }

        public void ResetToDefaults()
        {
            ModalDialogX.Value = -1;
            ModalDialogY.Value = -1;
            CurrencyValuationsJson.Value = string.Empty;
            ValueOwnMaterials.Value = true;
            ScrollDiagnosticsEnabled.Value = false;
        }
    }
}
