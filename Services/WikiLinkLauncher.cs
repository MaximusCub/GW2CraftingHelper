using System;
using System.Diagnostics;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// UI-bundle milestone, Feature A (wiki links) - this module's FIRST
    /// external-URL launch (deliberate maintainer decision, see the
    /// milestone spec). A thin Process.Start wrapper, deliberately kept
    /// separate from the pure, unit-tested WikiLinkBuilder: this class is
    /// side-effecting (spawns the user's default browser) and cannot be
    /// exercised by a real test the way WikiLinkBuilder's URL construction
    /// can, so it stays as small as possible and carries no logic of its
    /// own beyond "launch, and do not let a launch failure propagate into
    /// the caller's UI event handler".
    /// <para>
    /// net48/Process.Start(string) already resolves through ShellExecute
    /// (UseShellExecute defaults to true on this target framework), so a
    /// bare http(s) URL opens the OS's default browser directly - no
    /// ProcessStartInfo needed. Wrapped in try/catch because ShellExecute
    /// can throw (Win32Exception - no registered URL handler, a locked-down
    /// environment, etc.); a wiki-link click must never crash or otherwise
    /// disrupt the Blish HUD overlay it was clicked from.
    /// </para>
    /// </summary>
    public static class WikiLinkLauncher
    {
        public static void Open(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                // Services/ convention (see grep across this directory): no
                // Blish_HUD.Logger dependency here - ModuleLog.Shared is
                // this module's own Blish-free logging sink, already used
                // for the same "warn and keep going" shape elsewhere (e.g.
                // MainView.RefreshNowAsync's failure branch).
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "wiki", $"Failed to open wiki link: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
