using System;
using System.Collections.Generic;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// The Log tab's search/view pane (d2-log-system.md Section 3):
    /// level-filter dropdown, text search, follow-tail, copy-to-clipboard,
    /// and clear-view, backed directly by a ModuleLog's ring buffer.
    /// Pattern A (lightweight FlowPanel(CanScroll)) - label-per-row, no
    /// multi-column ellipsized rows that must reflow live during a resize
    /// drag - so this does not opt into the M33
    /// PlanContentHeightMath/relayout-registry contract (that machinery is
    /// CraftingPlanView-only, DO-NOT-TOUCH per M38).
    /// </summary>
    public class LogTabContent
    {
        private static readonly Logger Logger = Logger.GetLogger<LogTabContent>();

        private const int ToolbarHeight = 40;
        private const int LevelDropdownWidth = 90;
        private const int SearchBoxWidth = 220;
        private const int FollowCheckboxWidth = 90;
        private const int ButtonWidth = 100;
        private const int Gap = 8;

        private static readonly Color DebugColor = new Color(130, 130, 130);
        private static readonly Color InfoColor = new Color(210, 210, 210);
        private static readonly Color WarnColor = new Color(255, 200, 60);
        private static readonly Color ErrorColor = new Color(255, 100, 100);
        private static readonly Color EmptyStateColor = new Color(150, 150, 150);
        private static readonly Color StatusColor = new Color(170, 170, 170);

        private readonly ModuleLog _log;

        private Panel _toolbarPanel;
        private FlowPanel _contentPanel;
        private Dropdown _levelDropdown;
        private TextBox _searchBox;
        private Checkbox _followCheckbox;
        private StandardButton _copyButton;
        private StandardButton _clearViewButton;
        private Label _statusLabel;

        // Last Version this view rendered from - PollForUpdates uses this
        // to decide whether a rebuild is needed at all (a cheap long
        // compare on every frame the tab happens to be open, not a full
        // rebuild - d2 Section 4.3's "dirty-flag/Version poll" idiom).
        private long _lastSeenVersion = -1;

        // Set by "Clear view" to the ring Version at click time; any entry
        // whose absolute ring index is before this is hidden from the
        // CURRENT display only - the ring and the on-disk file are both
        // untouched (see ModuleLog.Clear / ModuleLogStore.DeleteAll for the
        // two genuinely destructive operations this deliberately is not -
        // d2 Section 7's "lifecycle - cleared on what").
        private long _clearedBeforeVersion;

        public LogTabContent(ModuleLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Build(Container container)
        {
            int w = container.ContentRegion.Width;

            _toolbarPanel = new Panel
            {
                Size = new Point(w, ToolbarHeight),
                Parent = container
            };

            _levelDropdown = new Dropdown
            {
                Size = new Point(LevelDropdownWidth, 30),
                Location = new Point(0, 5),
                Parent = _toolbarPanel
            };
            _levelDropdown.Items.Add("All");
            _levelDropdown.Items.Add("Error+");
            _levelDropdown.Items.Add("Warn+");
            _levelDropdown.Items.Add("Info+");
            _levelDropdown.Items.Add("Debug+");
            _levelDropdown.SelectedItem = "Info+"; // d2 Section 3 default
            _levelDropdown.ValueChanged += (_, __) => RebuildRows();

            _searchBox = new TextBox
            {
                Size = new Point(SearchBoxWidth, 26),
                Location = new Point(LevelDropdownWidth + Gap, 7),
                PlaceholderText = "Search...",
                Parent = _toolbarPanel
            };
            _searchBox.TextChanged += (_, __) => RebuildRows();

            _followCheckbox = new Checkbox
            {
                Text = "Follow",
                Checked = true, // d2 Section 3 default (ON)
                Size = new Point(FollowCheckboxWidth, 25),
                Location = new Point(LevelDropdownWidth + Gap + SearchBoxWidth + Gap, 8),
                Parent = _toolbarPanel
            };

            _clearViewButton = new StandardButton
            {
                Text = "Clear view",
                Size = new Point(ButtonWidth, 28),
                Parent = _toolbarPanel
            };
            _clearViewButton.Click += (_, __) => ClearView();

            _copyButton = new StandardButton
            {
                Text = "Copy",
                Size = new Point(ButtonWidth, 28),
                Parent = _toolbarPanel
            };
            _copyButton.Click += (_, __) => CopyToClipboard();

            _statusLabel = new Label
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = StatusColor,
                Location = new Point(LevelDropdownWidth + Gap + SearchBoxWidth + Gap + FollowCheckboxWidth + Gap, 12),
                Parent = _toolbarPanel
            };

            _contentPanel = new FlowPanel
            {
                Size = new Point(w, container.ContentRegion.Height - ToolbarHeight),
                Location = new Point(0, ToolbarHeight),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = container
            };

            PositionToolbarButtons(w);

            container.Resized += (_, __) =>
            {
                int newWidth = container.ContentRegion.Width;
                _toolbarPanel.Size = new Point(newWidth, ToolbarHeight);
                _contentPanel.Size = new Point(newWidth, container.ContentRegion.Height - ToolbarHeight);
                PositionToolbarButtons(newWidth);
            };

            _lastSeenVersion = _log.Version;
            RebuildRows();
        }

        private void PositionToolbarButtons(int w)
        {
            _copyButton.Location = new Point(w - (ButtonWidth * 2) - (Gap * 2), 5);
            _clearViewButton.Location = new Point(w - ButtonWidth - Gap, 5);
        }

        /// <summary>
        /// Called from Module.Update() only while this tab is the selected
        /// one (a cheap Version compare when nothing changed) - the "PLUS a
        /// poll" half of d2 Section 4.3's refresh design, on top of the
        /// TabChanged-driven <see cref="Refresh"/> below. Rebuilds only
        /// when Follow is checked; an unchecked Follow freezes the current
        /// view exactly like a paused `tail -f`, even though new entries
        /// keep arriving in the ring underneath it.
        /// </summary>
        public void PollForUpdates()
        {
            if (!IsLive)
            {
                return;
            }

            long currentVersion = _log.Version;
            if (currentVersion == _lastSeenVersion)
            {
                return;
            }

            _lastSeenVersion = currentVersion;

            if (_followCheckbox != null && _followCheckbox.Checked)
            {
                RebuildRows();
            }
        }

        /// <summary>
        /// Full rebuild from the ring, respecting the current filter state.
        /// Called on tab switch (Module.cs's TabChanged handler) regardless
        /// of Follow - re-opening the tab should always show current
        /// reality, not a frozen view from before the tab was last closed.
        /// </summary>
        public void Refresh()
        {
            if (!IsLive)
            {
                return;
            }

            _lastSeenVersion = _log.Version;
            RebuildRows();
        }

        /// <summary>
        /// True once <see cref="Build"/> has run and the content panel is
        /// still attached to a live control tree. A disposed control's
        /// Parent is nulled on disposal (the same "was this torn down"
        /// signal MainView.cs's async Refresh Now handler already relies
        /// on) - guards PollForUpdates/Refresh/RebuildRows against running
        /// against a panel whose tab was closed (or the whole window
        /// disposed on Module.Unload) since this instance's Build() ran;
        /// Module.Update()'s own SelectedTab/_logContent-null checks catch
        /// the common case, but do not cover the window being disposed
        /// while SelectedTab still happens to equal the Log tab.
        /// </summary>
        private bool IsLive => _contentPanel != null && _contentPanel.Parent != null;

        private void ClearView()
        {
            _clearedBeforeVersion = _log.Version;
            RebuildRows();
        }

        private void CopyToClipboard()
        {
            var result = GetFilteredEntries();
            if (result.Filtered.Count == 0)
            {
                SetStatus("Nothing to copy", isError: false);
                return;
            }

            try
            {
                // Fully-qualified rather than "using System.Windows.Forms;"
                // - that using would collide with Blish_HUD.Controls.TextBox
                // (both namespaces define a TextBox type), the same reason
                // CraftingPlanView.cs fully-qualifies
                // System.Windows.Forms.SystemInformation instead of adding
                // the using.
                string text = string.Join(Environment.NewLine, result.Filtered.Select(f => f.Line));
                System.Windows.Forms.Clipboard.SetText(text);
                SetStatus($"Copied {result.Filtered.Count} line{(result.Filtered.Count == 1 ? "" : "s")}", isError: false);
            }
            catch (Exception ex)
            {
                // d2 Section 3/Open Question 1: clipboard access can throw
                // (e.g. STA/COM issues, another app holding the clipboard) -
                // degrade to a visible status message rather than an
                // unhandled exception on the UI thread.
                Logger.Warn(ex, "Failed to copy log lines to clipboard");
                SetStatus("Copy failed - clipboard unavailable", isError: true);
            }
        }

        private void SetStatus(string text, bool isError)
        {
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.Text = text;
            _statusLabel.TextColor = isError ? ErrorColor : StatusColor;
        }

        private void RebuildRows()
        {
            if (!IsLive)
            {
                return;
            }

            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }

            var result = GetFilteredEntries();

            foreach (var item in result.Filtered)
            {
                new Label
                {
                    Text = item.Line,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    TextColor = ColorForLevel(item.Entry.Level),
                    Parent = _contentPanel
                };
            }

            if (result.Filtered.Count == 0)
            {
                // Distinguishes "ring genuinely empty" from "ring has data,
                // filter excludes all of it" (d2 Section 3's empty-state
                // spec) - RawCount is the ring's own unfiltered count, from
                // BEFORE the clearedBeforeVersion/level/search filters ran.
                new Label
                {
                    Text = result.RawCount == 0 ? "No log entries yet." : "No entries match the current filter.",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(8, 8),
                    TextColor = EmptyStateColor,
                    Parent = _contentPanel
                };
            }
            else if (_followCheckbox != null && _followCheckbox.Checked)
            {
                // Scroll-to-bottom via VerticalScrollOffset's public setter
                // (confirmed present on Blish_HUD.Controls.Panel) -
                // deliberately NOT the private-field Scrollbar.ScrollDistance
                // reflection CraftingPlanView needs for its own much more
                // exacting restore/verify contract (KNOWN-ISSUES #12/#14);
                // this tab carries none of that contract, so the simple
                // public property is the correct, far cheaper choice.
                // Overshoots (int.MaxValue) rather than measuring exact
                // content height - a scroll offset past the real maximum
                // clamps to the bottom, landing there regardless of how
                // tall the freshly rebuilt content is.
                _contentPanel.VerticalScrollOffset = int.MaxValue;
            }
        }

        private (List<(ModuleLogEntry Entry, string Line)> Filtered, int RawCount) GetFilteredEntries()
        {
            var entries = _log.Snapshot(out long version);
            long startIndex = version - entries.Count;

            ModuleLogLevel minLevel = MinLevelForFilter();
            string search = _searchBox?.Text?.Trim() ?? string.Empty;

            var filtered = new List<(ModuleLogEntry Entry, string Line)>();
            for (int i = 0; i < entries.Count; i++)
            {
                long absoluteIndex = startIndex + i;
                if (absoluteIndex < _clearedBeforeVersion)
                {
                    continue;
                }

                var entry = entries[i];
                if (entry.Level < minLevel)
                {
                    continue;
                }

                string line = FormatLine(entry);
                if (search.Length > 0 &&
                    line.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                filtered.Add((entry, line));
            }

            return (filtered, entries.Count);
        }

        private ModuleLogLevel MinLevelForFilter()
        {
            switch (_levelDropdown?.SelectedItem)
            {
                case "Error+": return ModuleLogLevel.Error;
                case "Warn+": return ModuleLogLevel.Warn;
                case "Info+": return ModuleLogLevel.Info;
                case "Debug+": return ModuleLogLevel.Debug;
                default: return ModuleLogLevel.Debug; // "All" (or unset)
            }
        }

        private static string FormatLine(ModuleLogEntry entry)
        {
            string levelText = entry.Level.ToString().ToUpperInvariant();
            string tagPart = string.IsNullOrEmpty(entry.Tag) ? string.Empty : $"[{entry.Tag}] ";
            return $"[{levelText}] {entry.TimestampUtc.ToLocalTime():HH:mm:ss} {tagPart}{entry.Message}";
        }

        private static Color ColorForLevel(ModuleLogLevel level)
        {
            switch (level)
            {
                case ModuleLogLevel.Error: return ErrorColor;
                case ModuleLogLevel.Warn: return WarnColor;
                case ModuleLogLevel.Debug: return DebugColor;
                default: return InfoColor;
            }
        }
    }
}
