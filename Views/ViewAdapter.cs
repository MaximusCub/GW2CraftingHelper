using System;
using System.Linq;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// Bridges plain Build(Container) classes to the IView interface
    /// required by TabbedWindow2.Tab. Wraps any Action&lt;Container&gt;
    /// as a View so existing MainView, CraftingPlanView, etc. work
    /// with TabbedWindow2 without conversion.
    /// </summary>
    public class ViewAdapter : View
    {
        private readonly Action<Container> _buildAction;

        public ViewAdapter(Action<Container> buildAction)
        {
            _buildAction = buildAction ?? throw new ArgumentNullException(nameof(buildAction));
        }

        protected override void Build(Container buildPanel)
        {
            // Defensive: clear any existing children before rebuilding.
            // TabbedWindow2 may provide a fresh container on each tab switch,
            // but we clear explicitly to prevent duplicate child controls if
            // the same container is ever reused across rebuilds.
            foreach (var child in buildPanel.Children.ToArray())
            {
                child.Dispose();
            }

            _buildAction(buildPanel);
        }
    }
}
