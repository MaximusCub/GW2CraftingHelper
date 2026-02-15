using Blish_HUD.Settings;

namespace GW2CraftingHelper.Services
{
    public class ModuleSettings
    {
        public SettingEntry<int> ModalDialogX { get; private set; }
        public SettingEntry<int> ModalDialogY { get; private set; }

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
        }

        public void ResetToDefaults()
        {
            ModalDialogX.Value = -1;
            ModalDialogY.Value = -1;
        }
    }
}
