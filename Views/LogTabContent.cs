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

        // Last Version this view has fully rendered up to (via either
        // RebuildRows or AppendNewRows) - PollForUpdates uses this both to
        // decide whether any work is needed at all (a cheap long compare on
        // every frame the tab happens to be open, not a full rebuild - d2
        // Section 4.3's "dirty-flag/Version poll" idiom) and as the
        // lower bound for the next incremental append. Always set to the
        // EXACT version the rendering method itself just read from
        // ModuleLog.Snapshot - never pre-set by a caller with a possibly
        // stale value - so two consecutive appends can never double-render
        // (or skip) an entry landed in the gap between reading Version here
        // and re-reading it inside Snapshot.
        private long _lastSeenVersion = -1;

        // True once RebuildRows or AppendNewRows has left at least one real
        // row (not the empty-state Label) in the content panel. Gates
        // whether PollForUpdates may use the incremental AppendNewRows path
        // at all - see PollForUpdates' own doc comment for why the
        // empty-to-non-empty transition still always goes through a full
        // RebuildRows.
        private bool _hasRenderedAnyRow;

        // FIFO of every currently-displayed "real" row (never the
        // empty-state Label), oldest-first, each tagged with the absolute
        // ring index it was rendered from. AppendNewRows enqueues onto the
        // back and RebuildRows rebuilds this from scratch; both then trim
        // from the front any row whose absolute index has since fallen
        // below the ring's own current earliest-available index (i.e. the
        // underlying entry was evicted from the ring). Without this, the
        // incremental append path would leave stale rows on screen forever
        // and grow _contentPanel's Label count without bound over a long
        // session - append-only alone only solves the "every frame" cost
        // the design doc warned about, not eviction.
        private readonly Queue<(long AbsoluteIndex, Label Control)> _renderedRows = new Queue<(long AbsoluteIndex, Label Control)>();

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

            RebuildRows();
        }

        private void PositionToolbarButtons(int w)
        {
            _copyButton.Location = new Point(w - (ButtonWidth * 2) - (Gap * 2), 5);
            _clearViewButton.Location = new Point(w - ButtonWidth - Gap, 5);
        }

        /// <summary>
        /// Called from Module.Update() only while this tab is the selected
        /// one (a cheap Version compare when nothing changed, and a no-op
        /// at all while Follow is unchecked) - the "PLUS a poll" half of d2
        /// Section 4.3's refresh design, on top of the TabChanged-driven
        /// <see cref="Refresh"/> below. An unchecked Follow freezes the
        /// current view exactly like a paused `tail -f`, even though new
        /// entries keep arriving in the ring underneath it.
        /// <para>
        /// When Follow IS checked and new entries arrived, this uses the
        /// incremental <see cref="AppendNewRows"/> path (d2 Section 4.3:
        /// "append-only incremental update... rather than the full-rebuild
        /// Refresh() on every version bump") instead of tearing down and
        /// recreating every already-visible row - unless the panel is
        /// currently showing the empty-state Label instead of real rows
        /// (<see cref="_hasRenderedAnyRow"/> false), which has no
        /// incremental equivalent and still falls back to a full
        /// <see cref="RebuildRows"/>. Full rebuild otherwise stays reserved
        /// for the filter-changed paths (level dropdown / search box /
        /// Clear view) and for <see cref="Refresh"/>'s own tab-switch case.
        /// </para>
        /// </summary>
        public void PollForUpdates()
        {
            if (!IsLive)
            {
                return;
            }

            if (_followCheckbox == null || !_followCheckbox.Checked)
            {
                return;
            }

            long currentVersion = _log.Version;
            if (currentVersion == _lastSeenVersion)
            {
                return;
            }

            if (_hasRenderedAnyRow)
            {
                AppendNewRows();
            }
            else
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

        /// <summary>
        /// Full dispose+rebuild from the ring, respecting the current
        /// filter state. Reserved for the filter-changed paths (level
        /// dropdown / search box / Clear view), <see cref="Refresh"/>'s
        /// tab-switch case, and the empty-to-non-empty transition
        /// <see cref="AppendNewRows"/> cannot handle incrementally - NOT
        /// called on every live Version bump while the tab is open and
        /// Follow is checked; see <see cref="PollForUpdates"/> for that
        /// path.
        /// </summary>
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
            _renderedRows.Clear();

            var result = GetFilteredEntries();
            _lastSeenVersion = result.Version;

            foreach (var item in result.Filtered)
            {
                var label = new Label
                {
                    Text = item.Line,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    TextColor = ColorForLevel(item.Entry.Level),
                    Parent = _contentPanel
                };
                _renderedRows.Enqueue((item.AbsoluteIndex, label));
            }

            _hasRenderedAnyRow = result.Filtered.Count > 0;

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
                ScrollToBottom();
            }
        }

        /// <summary>
        /// Incremental counterpart to <see cref="RebuildRows"/> (d2 Section
        /// 4.3): appends Label controls only for entries newer than
        /// <see cref="_lastSeenVersion"/>, instead of tearing down and
        /// recreating every already-visible row. Only ever called from
        /// <see cref="PollForUpdates"/>, and only while
        /// <see cref="_hasRenderedAnyRow"/> is true - see that method's own
        /// doc comment for why the empty-state transition is excluded.
        /// <para>
        /// Also trims from the FRONT of <see cref="_renderedRows"/> any row
        /// whose underlying ring entry has since been evicted (the ring
        /// only holds its own capacity's worth of history) - append-only
        /// with no matching removal would otherwise leave stale rows on
        /// screen and grow the panel's Label count without bound over a
        /// long session, which is exactly the kind of unbounded-retention
        /// regression the append fix must not trade the every-frame-cost
        /// regression for.
        /// </para>
        /// </summary>
        private void AppendNewRows()
        {
            if (!IsLive)
            {
                return;
            }

            long previousVersion = _lastSeenVersion;
            var entries = _log.Snapshot(out long version);
            long startIndex = version - entries.Count;

            // Clamp to whatever the ring can still produce - if more
            // entries arrived between polls than the ring holds, the
            // oldest of those were evicted before this view (or any view)
            // ever had a chance to render them; there is nothing to append
            // for them either way, incrementally or otherwise.
            long from = Math.Max(previousVersion, startIndex);

            ModuleLogLevel minLevel = MinLevelForFilter();
            string search = _searchBox?.Text?.Trim() ?? string.Empty;
            bool appendedAny = false;

            for (long absoluteIndex = from; absoluteIndex < version; absoluteIndex++)
            {
                if (absoluteIndex < _clearedBeforeVersion)
                {
                    continue;
                }

                var entry = entries[(int)(absoluteIndex - startIndex)];
                if (!TryFormatFiltered(entry, minLevel, search, out string line))
                {
                    continue;
                }

                var label = new Label
                {
                    Text = line,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    TextColor = ColorForLevel(entry.Level),
                    Parent = _contentPanel
                };
                _renderedRows.Enqueue((absoluteIndex, label));
                appendedAny = true;
            }

            while (_renderedRows.Count > 0 && _renderedRows.Peek().AbsoluteIndex < startIndex)
            {
                var stale = _renderedRows.Dequeue();
                stale.Control.Dispose();
            }

            _lastSeenVersion = version;
            _hasRenderedAnyRow = _renderedRows.Count > 0;

            if (appendedAny && _followCheckbox != null && _followCheckbox.Checked)
            {
                ScrollToBottom();
            }
        }

        /// <summary>
        /// Scroll-to-bottom via VerticalScrollOffset's public setter
        /// (confirmed present on Blish_HUD.Controls.Panel) - deliberately
        /// NOT the private-field Scrollbar.ScrollDistance reflection
        /// CraftingPlanView needs for its own much more exacting
        /// restore/verify contract (KNOWN-ISSUES #12/#14); this tab carries
        /// none of that contract, so the simple public property is the
        /// correct, far cheaper choice. Overshoots (int.MaxValue) rather
        /// than measuring exact content height - a scroll offset past the
        /// real maximum clamps to the bottom, landing there regardless of
        /// how tall the content is after a rebuild or append.
        /// </summary>
        private void ScrollToBottom()
        {
            _contentPanel.VerticalScrollOffset = int.MaxValue;
        }

        private (List<(ModuleLogEntry Entry, string Line, long AbsoluteIndex)> Filtered, int RawCount, long Version) GetFilteredEntries()
        {
            var entries = _log.Snapshot(out long version);
            long startIndex = version - entries.Count;

            ModuleLogLevel minLevel = MinLevelForFilter();
            string search = _searchBox?.Text?.Trim() ?? string.Empty;

            var filtered = new List<(ModuleLogEntry Entry, string Line, long AbsoluteIndex)>();
            for (int i = 0; i < entries.Count; i++)
            {
                long absoluteIndex = startIndex + i;
                if (absoluteIndex < _clearedBeforeVersion)
                {
                    continue;
                }

                var entry = entries[i];
                if (!TryFormatFiltered(entry, minLevel, search, out string line))
                {
                    continue;
                }

                filtered.Add((entry, line, absoluteIndex));
            }

            return (filtered, entries.Count, version);
        }

        /// <summary>
        /// Shared level/search filter test used by both
        /// <see cref="GetFilteredEntries"/> (full rebuild) and
        /// <see cref="AppendNewRows"/> (incremental append), so the two
        /// rendering paths can never silently diverge on which entries
        /// count as "visible". Returns false (with <paramref name="line"/>
        /// set to null) when <paramref name="entry"/> fails either filter.
        /// </summary>
        private static bool TryFormatFiltered(ModuleLogEntry entry, ModuleLogLevel minLevel, string search, out string line)
        {
            line = null;

            if (entry.Level < minLevel)
            {
                return false;
            }

            string candidate = FormatLine(entry);
            if (search.Length > 0 &&
                candidate.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            line = candidate;
            return true;
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
