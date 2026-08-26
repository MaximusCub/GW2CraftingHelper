using Blish_HUD.Controls;
using System;

namespace GW2CraftingHelper.Views
{
    internal class AutocompleteEnterEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }

    internal class AutocompleteTextBox : TextBox
    {
        public event EventHandler<int> ArrowPressed;

        public event EventHandler<AutocompleteEnterEventArgs> EnterKeyPressed;

        protected override void MoveLine(int delta)
        {
            var handler = ArrowPressed;
            if (handler != null)
            {
                handler(this, delta);
                return;
            }

            base.MoveLine(delta);
        }

        protected override void HandleEnter()
        {
            var handler = EnterKeyPressed;
            if (handler != null)
            {
                var args = new AutocompleteEnterEventArgs();
                handler(this, args);
                if (args.Handled)
                {
                    return;
                }
            }

            base.HandleEnter();
        }
    }
}
