using System;
using System.Diagnostics;
using System.Threading.Tasks;

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
    /// <para>
    /// Fix-pass (UI-thread stall): both call sites are mouse-event handlers
    /// dispatched from the game update loop, and ShellExecuteEx blocks the
    /// calling thread until the shell hands the URL off - a cold browser
    /// start, DDE negotiation, or a "choose an app" prompt can stall that
    /// call for hundreds of ms to seconds, freezing the whole overlay
    /// (scroll/relayout included) for as long as it runs. The actual
    /// Process.Start call is therefore offloaded to a background thread via
    /// Task.Run; the try/catch stays INSIDE the task (not wrapped around
    /// Task.Run itself) so a launch failure is still caught and logged here
    /// rather than becoming an unobserved task exception.
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

            // every current caller only ever passes a
            // WikiLinkBuilder result (always BaseUrl-prefixed), but this is
            // the module's first shell-out and Process.Start(string) on
            // net48 resolves through ShellExecute (UseShellExecute
            // defaults to true), which will happily launch a local
            // executable, a UNC path, or a file:/custom-scheme handler.
            // Guarding here keeps the safety property at the launch site
            // rather than depending on every present and future caller.
            if (!url.StartsWith("https://", StringComparison.Ordinal))
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    // dispose the handle ShellExecute hands
                    // back on a successful launch - discarding it undisposed
                    // leaks a process handle per click in a long-running
                    // overlay. The ShellExecute path can return null here;
                    // `using` tolerates that.
                    using (Process.Start(url))
                    {
                    }
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
            });
        }
    }
}
