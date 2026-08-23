using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// The Log tab's search/view pane (d2-log-system.md Section 3):
    /// level-filter dropdown, text search, follow-tail, copy-to-clipboard,
    /// clear-view, and the confirm-gated destructive delete-log-file
    /// action, backed directly by a ModuleLog's ring buffer.
    /// Pattern A (lightweight FlowPanel(CanScroll)): every row is a
    /// fixed-height Panel of two ellipsized columns (dim prefix, message),
    /// re-fitted by walking <see cref="_renderedRows"/> straight from the
    /// container's own Resized handler. That is deliberately NOT the
    /// PlanContentHeightMath/relayout-registry contract (that machinery is
    /// CraftingPlanView-only): rows here are uniform height, so there is no
    /// per-section height math to keep in sync, and the tail-follow scroll
    /// overshoots rather than restoring an exact offset, so there is no
    /// settle/verify pass to defer the re-ellipsis into.
    /// </summary>
    public class LogTabContent
    {
        private static readonly Logger Logger = Logger.GetLogger<LogTabContent>();

        private const int ToolbarHeight = 40;
        private const int LevelDropdownWidth = 90;
        private const int SearchBoxWidth = 220;
        private const int FollowCheckboxWidth = 90;
        private const int ButtonWidth = 100;
        // Wider than ButtonWidth - "Delete Log File" is deliberately
        // spelled out in full so the destructive scope is unmistakable
        // next to the view-only "Clear View".
        private const int DeleteButtonWidth = 120;
        private const int Gap = 8;

        // Full-width status row beneath the toolbar, mirroring MainView's
        // own _statusPanel: the status label is auto-sized, so sharing the
        // toolbar row with the three right-anchored buttons ran a long
        // status underneath them at the module's default width.
        private const int StatusRowHeight = 24;

        // The FlowPanel scrolls, so a row sized to the panel's full width
        // would run under the scrollbar strip. Same allowance MainView's
        // own scrolling source-filter flow uses.
        private const int ScrollbarAllowance = 20;

        // Prefix column allowance for the "[tag]" part, counted in 'w'
        // glyphs and sized off the longest tag written anywhere in the tree:
        // "snapshot-fetch", 14 characters. The margin is the glyph itself -
        // every tag in this module is lowercase ASCII plus '-', all narrower
        // than 'w' - so a somewhat longer tag still fits; anything past that
        // ellipsizes rather than widening the column and pushing every
        // message right. Load-bearing: too small an allowance renders the
        // module's most common WARN source permanently truncated (and
        // permanently tooltip-flagged) at every window width.
        private const int TagColumnChars = 14;

        // The prefix is chrome, not content - dimmed so the message reads
        // first, but still carrying the level tint so severity is legible
        // at a glance down the column. Alpha-scaling matches this repo's
        // existing dim idiom (TreeSectionController's `Color.White * 0.35f`).
        private const float PrefixDimFactor = 0.7f;

        private static readonly Color DebugColor = new Color(130, 130, 130);
        private static readonly Color InfoColor = new Color(210, 210, 210);
        private static readonly Color WarnColor = new Color(255, 200, 60);
        private static readonly Color ErrorColor = new Color(255, 100, 100);
        private static readonly Color EmptyStateColor = new Color(150, 150, 150);
        private static readonly Color StatusColor = new Color(170, 170, 170);

        private readonly ModuleLog _log;
        private readonly ModalDialog _modalDialog;

        private Panel _toolbarPanel;
        private Panel _statusPanel;
        private FlowPanel _contentPanel;
        private Dropdown _levelDropdown;
        private TextBox _searchBox;
        private Checkbox _followCheckbox;
        private StandardButton _copyButton;
        private StandardButton _clearViewButton;
        private StandardButton _deleteFileButton;
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

        // True once Build's own initial RebuildRows call (see the bottom
        // of Build) has finished. Originally added (PR #99) to guard
        // PollForUpdates against a real race: per docs/ARCHITECTURE.md
        // Section 1, Blish HUD's own WindowBase2.ShowView runs a tab's
        // Build() via View.DoLoad().ContinueWith(...) - with no
        // SynchronizationContext installed, that continuation resumes on a
        // ThreadPool thread, not the main/game thread. Without this guard,
        // PollForUpdates() (main thread, driven by Module.Update() as soon
        // as SelectedTab flips to the Log tab) could invoke RebuildRows()
        // concurrently with Build()'s own tail RebuildRows() call, on the
        // SAME freshly-created _contentPanel - this produced two stacked
        // "No log entries yet." placeholders, confirmed live.
        // <para>
        // A second path reaches the SAME hazard:
        // Module.cs's TabChanged handler also calls Refresh() ->
        // RebuildRows() synchronously on the main thread whenever the Log
        // tab becomes selected; without this latch, Build()'s
        // ThreadPool-thread RebuildRows() call
        // and TabChanged's main-thread RebuildRows() call landed on the
        // SAME instance at the same time, and two threads concurrently
        // Enqueue-ing into _renderedRows corrupted its internal array,
        // crashing with "Destination array was not long enough" inside
        // Queue&lt;T&gt;.SetCapacity.
        // </para>
        // <para>
        // Fix: Build()'s own tail (the RebuildRows() call plus the write to
        // this field) is now marshaled onto the main thread via
        // MainThreadMarshal.Run (see Build()'s own comment) - so it, along
        // with PollForUpdates() and Refresh() (both main-thread-only
        // already), can never execute concurrently with anything: a single
        // thread cannot run two call stacks at the same instant, so the
        // race is impossible BY CONSTRUCTION, not merely guarded. This
        // field is KEPT (not removed as obsolete) - it still gates
        // PollForUpdates(), Refresh(), the level dropdown/search box
        // ValueChanged/TextChanged handlers, and ClearView() against acting
        // before Build()'s own queued tail has actually landed, which
        // avoids a wasted, redundant RebuildRows() pass rather than a crash
        // now - belt-and-braces, not a safety requirement any more.
        // volatile is REMOVED: every read and write of this field now
        // happens on the main thread only (Build()'s write runs inside the
        // MainThreadMarshal.Run callback), so there is no remaining
        // cross-thread visibility concern for volatile to address, and
        // keeping it would misleadingly suggest this field is still
        // accessed from more than one thread.
        // </para>
        // <para>
        // PRECISE INVARIANT (the claim this fix actually establishes, not a
        // broader one): <see cref="_renderedRows"/>, <see
        // cref="_lastSeenVersion"/>, <see cref="_hasRenderedAnyRow"/>,
        // <see cref="_fullPrefixWidth"/>, <see cref="_lastLayoutWidth"/>, the
        // Module-owned "Clear View" floor reached via
        // <see cref="_getClearedBeforeVersion"/>/
        // <see cref="_setClearedBeforeVersion"/>, this field, and
        // _contentPanel's Children collection are MAIN-THREAD-ONLY - every
        // entry point that touches them (Build's marshaled tail,
        // PollForUpdates, Refresh, the level dropdown/search box handlers
        // via RebuildRowsIfBuilt, ClearView, and the container Resized
        // handler) runs on the main thread, and the five of those (every
        // one except Build's own tail, which IS the thing being awaited)
        // additionally defer to Build's tail rather than acting while it is
        // still pending. This is narrower than "every field this class
        // touches is main-thread-only": the control fields (_toolbarPanel,
        // _levelDropdown, _searchBox, _followCheckbox, _clearViewButton,
        // _copyButton, _deleteFileButton, _statusPanel, _statusLabel,
        // _contentPanel) are still first
        // PUBLISHED by the rest of Build()'s body on the ThreadPool thread,
        // same as every Blish view in this module - any main-thread read of
        // one of them (e.g. CopyToClipboard/SetStatus reading
        // _statusLabel, which does not touch any of the state above and so
        // is deliberately NOT gated on this field) must stay behind its
        // existing null guard (IsLive, _searchBox?, _statusLabel == null)
        // rather than assume the field is already non-null.
        // </para>
        private bool _buildComplete;

        // FIFO of every currently-displayed "real" row (never the
        // empty-state Label), oldest-first, each tagged with the absolute
        // ring index it was rendered from. AppendNewRows enqueues onto the
        // back and RebuildRows rebuilds this from scratch; both then trim
        // from the front any row whose absolute index has since fallen
        // below the ring's own current earliest-available index (i.e. the
        // underlying entry was evicted from the ring). Without this, the
        // incremental append path would leave stale rows on screen forever
        // and grow _contentPanel's child count without bound over a long
        // session - append-only alone only solves the "every frame" cost
        // the design doc warned about, not eviction.
        // It is also what the container's Resized handler walks to re-fit
        // every visible row's two columns to the new width.
        private readonly Queue<LogRow> _renderedRows = new Queue<LogRow>();

        /// <summary>
        /// One rendered entry: a fixed-height Panel holding a dim
        /// prefix Label ("[LEVEL] timestamp [tag]") at x=0 and the message
        /// Label at the shared message-column x, each ellipsized to its own
        /// column. The full strings are kept alongside the controls because
        /// both are needed again on every resize - re-ellipsizing from
        /// Label.Text would compound "..." onto already-truncated text - and
        /// <see cref="FullLine"/> is the tooltip a shortened row carries,
        /// stored already wrapped through the tooltip facility's seam so the
        /// per-render change guard in UpdateRow can compare against it
        /// directly.
        /// </summary>
        private sealed class LogRow
        {
            internal long AbsoluteIndex;
            internal Panel Panel;
            internal Label PrefixLabel;
            internal Label MessageLabel;
            internal string FullPrefix;
            internal string FullMessage;
            internal string FullLine;
        }

        // The "Clear View" floor must not be a plain instance field
        // here: Blish constructs a brand new LogTabContent on every tab
        // visit, so an instance field resets and a "Clear View" click
        // silently undoes itself on the next tab switch.
        // Moved onto Module itself (Module._logViewClearedBeforeVersion -
        // see that field's own doc comment for the full threading
        // rationale), accessed here through this getter/setter delegate
        // pair - mirrors TreeSectionController's own constructor-injected
        // getter/setter pattern for state that outlives a single render
        // (CraftingPlanView's _currentPlan get/set pair) rather than
        // introducing a new holder type. ClearView() calls
        // _setClearedBeforeVersion; GetFilteredEntries (RebuildRows' own
        // helper) and AppendNewRows call _getClearedBeforeVersion - both
        // only from this class's existing main-thread-only entry points
        // (Build's MainThreadMarshal.Run tail, PollForUpdates, Refresh,
        // RebuildRowsIfBuilt), so no new threading exposure versus the
        // field this replaces.
        private readonly Func<long> _getClearedBeforeVersion;
        private readonly Action<long> _setClearedBeforeVersion;

        public LogTabContent(ModuleLog log, ModalDialog modalDialog, Func<long> getClearedBeforeVersion, Action<long> setClearedBeforeVersion)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _modalDialog = modalDialog ?? throw new ArgumentNullException(nameof(modalDialog));
            _getClearedBeforeVersion = getClearedBeforeVersion ?? throw new ArgumentNullException(nameof(getClearedBeforeVersion));
            _setClearedBeforeVersion = setClearedBeforeVersion ?? throw new ArgumentNullException(nameof(setClearedBeforeVersion));
        }

        public void Build(Container container)
        {
            int w = container.ContentRegion.Width;

            _toolbarPanel = new Panel
            {
                Size = new Point(w, ToolbarHeight),
                Parent = container
            };

            // Textbox first, then the dropdown that narrows it - the
            // order the Snapshot tab's own search row uses. This row read
            // dropdown-then-textbox, so the module's two search rows were
            // mirror images of each other (audit batch J, M12).
            _searchBox = new TextBox
            {
                Size = new Point(SearchBoxWidth, 26),
                Location = new Point(0, 7),
                PlaceholderText = "Search log entries...",
                Parent = _toolbarPanel
            };
            _searchBox.TextChanged += (_, __) => RebuildRowsIfBuilt();

            _levelDropdown = new Dropdown
            {
                Size = new Point(LevelDropdownWidth, 30),
                Location = new Point(SearchBoxWidth + Gap, 5),
                Parent = _toolbarPanel
            };
            _levelDropdown.Items.Add("All");
            _levelDropdown.Items.Add("Error+");
            _levelDropdown.Items.Add("Warn+");
            _levelDropdown.Items.Add("Info+");
            _levelDropdown.Items.Add("Debug+");
            _levelDropdown.SelectedItem = "Info+"; // d2 Section 3 default
            _levelDropdown.ValueChanged += (_, __) => RebuildRowsIfBuilt();

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
                Text = "Clear View",
                Size = new Point(ButtonWidth, 28),
                Parent = _toolbarPanel
            };
            TooltipFacility.ApplyPlain(
                _clearViewButton,
                "Hide current entries from this view. New entries still appear; the log file keeps everything.");
            _clearViewButton.Click += (_, __) => ClearView();

            _copyButton = new StandardButton
            {
                Text = "Copy",
                Size = new Point(ButtonWidth, 28),
                Parent = _toolbarPanel
            };
            _copyButton.Click += (_, __) => CopyToClipboard();

            _deleteFileButton = new StandardButton
            {
                Text = "Delete Log File",
                Size = new Point(DeleteButtonWidth, 28),
                Parent = _toolbarPanel
            };
            TooltipFacility.ApplyPlain(
                _deleteFileButton,
                "Permanently delete the log file from disk and clear the in-memory log. Cannot be undone.");
            _deleteFileButton.Click += (_, __) => ConfirmDeleteLogFile();

            _statusPanel = new Panel
            {
                Size = new Point(w, StatusRowHeight),
                Location = new Point(0, ToolbarHeight),
                Parent = container
            };

            _statusLabel = new Label
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = StatusColor,
                // Y=2 inside this 24px row, matching MainView's own status
                // label for the same DefaultFont14 clearance.
                Location = new Point(0, 2),
                Parent = _statusPanel
            };

            _contentPanel = new FlowPanel
            {
                Size = new Point(w, container.ContentRegion.Height - ToolbarHeight - StatusRowHeight),
                Location = new Point(0, ToolbarHeight + StatusRowHeight),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = container
            };

            PositionToolbarButtons(w);

            container.Resized += (_, __) =>
            {
                int newWidth = container.ContentRegion.Width;
                _toolbarPanel.Size = new Point(newWidth, ToolbarHeight);
                _statusPanel.Size = new Point(newWidth, StatusRowHeight);
                _contentPanel.Size = new Point(newWidth, container.ContentRegion.Height - ToolbarHeight - StatusRowHeight);
                PositionToolbarButtons(newWidth);

                // After the panel's own resize, so RefitRows reads the new
                // width from it rather than being handed one.
                RefitRows();
            };

            // FIELD CRASH (docs/KNOWN-ISSUES.md): Build() itself
            // runs on a ThreadPool thread (see _buildComplete's own doc
            // comment for the DoLoad().ContinueWith(...) pattern), so
            // calling RebuildRows() directly here raced against Module.cs's
            // TabChanged handler calling Refresh() -> RebuildRows() on the
            // main thread against this SAME instance - two threads
            // concurrently Enqueue-ing into _renderedRows corrupted its
            // internal array and crashed with "Destination array was not
            // long enough" inside Queue<T>.SetCapacity. Marshaling this
            // tail onto the main thread, together with the _buildComplete
            // gate now on every other RebuildRows() entry point (below in
            // this file: PollForUpdates, Refresh, the level dropdown/search
            // box ValueChanged/TextChanged handlers, and ClearView), closes
            // the race BY CONSTRUCTION: every one of those six call sites
            // either runs on the main thread and defers to this queued tail
            // while _buildComplete is false, or IS this queued tail - so no
            // two of them can ever execute concurrently with each other.
            // This does NOT make every field this class touches main-
            // thread-only - see the correctness-invariant note on
            // _buildComplete's own doc comment for the narrower, precise
            // claim (the row-rendering state only) and what is deliberately
            // left OUTSIDE it (the control fields Build() publishes
            // below, on this same ThreadPool thread).
            MainThreadMarshal.Run(() =>
            {
                // If the module has been unloaded since this was queued,
                // RebuildRows()'s own IsLive check already no-ops safely
                // (Module.Unload disposes _mainWindow, which nulls every
                // control's Parent). A plain tab switch-away in the
                // meantime does NOT trip that guard - Blish's own
                // WindowBase2.ClearView only detaches (does not dispose)
                // the outgoing view's top-level panel, so _contentPanel
                // keeps a non-null Parent - see docs/ARCHITECTURE.md
                // Section 1 ("a tab switch detaches, it does not dispose").
                // This tail still renders correctly in that case; the
                // render just lands in a tree the user can no longer see -
                // wasted work, not a hazard.
                RebuildRows();

                // Must run in this SAME queued callback, after
                // RebuildRows() above - see _buildComplete's own doc
                // comment for why PollForUpdates/Refresh need this to stay
                // false until Build()'s own first real render has landed.
                _buildComplete = true;
            });
        }

        private void PositionToolbarButtons(int w)
        {
            // Delete Log File sits leftmost of the three so the two
            // view-only buttons keep their established right-edge spots
            // and the destructive one is not the easiest to reach.
            _deleteFileButton.Location = new Point(w - (ButtonWidth * 2) - DeleteButtonWidth - (Gap * 3), 5);
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
        /// entries keep arriving in the ring underneath it. Also a no-op
        /// until <see cref="_buildComplete"/> is set - see that field's own
        /// doc comment for what this guards against.
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
        /// Clear View) and for <see cref="Refresh"/>'s own tab-switch case.
        /// </para>
        /// </summary>
        public void PollForUpdates()
        {
            if (!_buildComplete)
            {
                // Build()'s own initial RebuildRows() (queued via
                // MainThreadMarshal - see Build()'s tail) has not landed on
                // the main thread yet, so _contentPanel is not fully
                // populated. Racing ahead with RebuildRows()/AppendNewRows()
                // here would be a wasted, redundant pass (originally, before
                // that queuing, it could also produce the doubled empty-
                // state placeholder - see _buildComplete's own doc comment);
                // skip this poll entirely and let Build()'s queued tail
                // render current reality on its own.
                return;
            }

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
        /// Gated on <see cref="_buildComplete"/> exactly like
        /// <see cref="PollForUpdates"/> - see that field's own doc comment
        /// for why (the field crash was this method racing
        /// Build()'s tail on two different threads; both are main-thread-
        /// only now, so the guard here is about avoiding a redundant
        /// rebuild, not a crash).
        /// </summary>
        public void Refresh()
        {
            if (!_buildComplete)
            {
                // Build()'s own initial RebuildRows() (queued via
                // MainThreadMarshal - see Build()'s tail) has not landed on
                // the main thread yet. Both this call and that queued
                // callback are main-thread-only now, so racing ahead here
                // could not corrupt anything - but it would still be a
                // wasted, redundant RebuildRows() pass against state
                // Build()'s own queued tail is about to overwrite anyway.
                // Skip and let Build()'s tail render current reality on its
                // own within the same or next Update() tick.
                return;
            }

            if (!IsLive)
            {
                return;
            }

            RebuildRows();
        }

        /// <summary>
        /// True once <see cref="Build"/> has run and the content panel has
        /// not been disposed. A disposed control's Parent is nulled on
        /// disposal (the same "was this torn down" signal MainView.cs's
        /// async Refresh Now handler already relies on) - guards
        /// PollForUpdates/Refresh/RebuildRows against running against a
        /// panel whose WINDOW was disposed on Module.Unload since this
        /// instance's Build() ran; Module.Update()'s own SelectedTab/
        /// _logContent-null checks catch the common case, but do not cover
        /// the window being disposed while SelectedTab still happens to
        /// equal the Log tab. This does NOT detect a plain tab
        /// switch-away, and never has - per docs/ARCHITECTURE.md Section 1
        /// ("a tab switch detaches, it does not dispose"), Blish's own
        /// ClearView() only detaches the outgoing view's top-level panel
        /// rather than disposing it, so _contentPanel keeps a non-null
        /// Parent (this property stays true) after the user switches away
        /// from this tab; a caller that still runs in that window reaches
        /// a real, live-but-unreachable panel, not a null one.
        /// </summary>
        private bool IsLive => _contentPanel != null && _contentPanel.Parent != null;

        /// <summary>
        /// Guarded entry point shared by the level dropdown's ValueChanged
        /// and the search box's TextChanged handlers (and, via
        /// <see cref="ClearView"/>, the Clear-view button) - the single
        /// source of truth for the <see cref="_buildComplete"/> gate so all
        /// three stay in sync with each other and with PollForUpdates/
        /// Refresh's own checks. Without this gate, one of these handlers
        /// firing while Build()'s body is still constructing the rest of
        /// the toolbar (mid-body, on the ThreadPool thread) would call
        /// RebuildRows() against whatever _contentPanel currently is,
        /// instead of leaving that render to Build()'s own marshaled tail -
        /// see _buildComplete's own doc comment for the full invariant.
        /// </summary>
        private void RebuildRowsIfBuilt()
        {
            if (_buildComplete)
            {
                RebuildRows();
            }
        }

        private void ClearView()
        {
            // The version write always takes effect immediately, even if
            // Build's own tail has not landed yet (see RebuildRowsIfBuilt
            // above) - it is a plain delegate-backed field write on Module
            // (see that field's own doc comment), not a control mutation,
            // so a pre-build Clear still hides everything before this point
            // once Build's tail does its own initial RebuildRows() pass. It
            // also survives this instance being torn down and a fresh
            // LogTabContent being built for the next tab visit.
            _setClearedBeforeVersion(_log.Version);
            RebuildRowsIfBuilt();
        }

        /// <summary>
        /// The destructive counterpart to <see cref="ClearView"/> (d2's
        /// Open Question 4): deletes the on-disk log file and clears the
        /// in-memory ring via <see cref="ModuleLog.DeleteFileAndReset"/>,
        /// behind the same ModalDialog confirm the Crafting Plan tab uses
        /// for its own regenerate gate. The confirm callback fires on the
        /// main thread (a ModalDialog button Click handler), but the
        /// destructive work runs on Task.Run: DeleteFileAndReset drains
        /// the flush queue (bounded) and then takes the file gate for real
        /// disk IO - a lock the background FlushLoop can hold through a
        /// slow append or full-file trim, exactly the cross-thread stall
        /// ModuleLog.Write's own doc comment forbids on a
        /// latency-sensitive thread. The status/rebuild tail marshals back
        /// via MainThreadMarshal.Run, the same thread every other rebuild
        /// entry point here already runs on.
        /// </summary>
        private void ConfirmDeleteLogFile()
        {
            _modalDialog.Show(
                "This permanently deletes the log file from disk. Continue?",
                () =>
                {
                    Task.Run(() =>
                    {
                        _log.DeleteFileAndReset();
                        MainThreadMarshal.Run(() =>
                        {
                            SetStatus("Log file deleted", isError: false);
                            RebuildRowsIfBuilt();
                        });
                    });
                },
                null,
                confirmText: "Delete");
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

            _statusLabel.TextColor = isError ? ErrorColor : StatusColor;
            _statusLabel.Text = text ?? "";
        }

        /// <summary>
        /// Full dispose+rebuild from the ring, respecting the current
        /// filter state. Reserved for the filter-changed paths (level
        /// dropdown / search box / Clear View), <see cref="Refresh"/>'s
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

            var metrics = MeasureRowMetrics();
            foreach (var item in result.Filtered)
            {
                CreateRow(item.Entry, item.Line, item.AbsoluteIndex, metrics);
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
        /// 4.3): appends rows only for entries newer than
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
        /// screen and grow the panel's child count without bound over a
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

            // Read once rather than per-iteration - the delegate call
            // itself is cheap, but the floor cannot change mid-loop (this
            // method is main-thread-only, same call stack throughout), so
            // there is no reason to re-invoke it every entry.
            long clearedBeforeVersion = _getClearedBeforeVersion();

            // Measured once per pass, not per row - see MeasureRowMetrics.
            var metrics = MeasureRowMetrics();

            for (long absoluteIndex = from; absoluteIndex < version; absoluteIndex++)
            {
                if (!LogViewFloor.IsVisible(absoluteIndex, clearedBeforeVersion))
                {
                    continue;
                }

                var entry = entries[(int)(absoluteIndex - startIndex)];
                if (!TryFormatFiltered(entry, minLevel, search, out string line))
                {
                    continue;
                }

                CreateRow(entry, line, absoluteIndex, metrics);
                appendedAny = true;
            }

            while (_renderedRows.Count > 0 && _renderedRows.Peek().AbsoluteIndex < startIndex)
            {
                var stale = _renderedRows.Dequeue();

                // Disposing the row Panel disposes its two Labels with it
                // (Container.Dispose walks its children), so the split does
                // not leak the halves of an evicted row.
                stale.Panel.Dispose();
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
        /// restore/verify contract; this tab carries
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

        /// <summary>
        /// Shared column geometry for one render pass. Measured once per
        /// pass rather than per row - every row in the panel shares the same
        /// prefix column (that shared column is the point: the messages line
        /// up so the eye can run down them) and the same fixed height.
        /// </summary>
        private struct RowMetrics
        {
            internal BitmapFont Font;
            internal int RowWidth;
            internal int RowHeight;
            internal int PrefixWidth;
            internal int MessageX;
            internal int MessageMaxWidth;
        }

        // Cached across passes: measuring the prefix template costs a
        // handful of MeasureString calls and the font never changes at
        // runtime. Main-thread-only, like every other row-rendering field
        // (see _buildComplete's own doc comment).
        private int _fullPrefixWidth;

        // Content width the currently-rendered rows were laid out against.
        // Written by MeasureRowMetrics (the single place that reads the
        // panel's width), read by RefitRows so a vertical-only resize drag -
        // which cannot change any column - costs nothing per row.
        private int _lastLayoutWidth = -1;

        private RowMetrics MeasureRowMetrics()
        {
            var font = GameService.Content.DefaultFont14;
            int contentWidth = _contentPanel?.Width ?? 0;
            _lastLayoutWidth = contentWidth;

            int rowWidth = Math.Max(0, contentWidth - ScrollbarAllowance);
            int prefixWidth = LogRowLayout.PrefixWidth(FullPrefixWidth(font), rowWidth);

            return new RowMetrics
            {
                Font = font,
                RowWidth = rowWidth,
                // +2 so a descender cannot touch the next row's ascender;
                // the panel clips its children, so a row shorter than its
                // own text would cut the text off rather than overflow.
                RowHeight = Measure(font, "Ag").Height + 2,
                PrefixWidth = prefixWidth,
                MessageX = LogRowLayout.MessageX(prefixWidth),
                MessageMaxWidth = LogRowLayout.MessageMaxWidth(rowWidth, prefixWidth)
            };
        }

        /// <summary>
        /// Width of the widest prefix the column ever has to hold:
        /// "[LEVEL] yyyy-MM-dd HH:mm:ss [tag]" with the widest level name,
        /// a timestamp built from the widest decimal digit, and a
        /// <see cref="TagColumnChars"/> tag allowance. Sizing from a
        /// worst-case template (rather than from the rows on screen) is what
        /// keeps the message column at the same x on every row and across
        /// both render paths - the incremental append sees only the new
        /// entries, so a width derived from what it can see would drift away
        /// from the rows a full rebuild produced.
        /// </summary>
        private int FullPrefixWidth(BitmapFont font)
        {
            if (_fullPrefixWidth > 0)
            {
                return _fullPrefixWidth;
            }

            char widestDigit = '0';
            int widestDigitWidth = 0;
            for (char digit = '0'; digit <= '9'; digit++)
            {
                int width = Measure(font, digit.ToString()).Width;
                if (width > widestDigitWidth)
                {
                    widestDigitWidth = width;
                    widestDigit = digit;
                }
            }

            string stamp = string.Format(
                CultureInfo.InvariantCulture,
                "{0}{0}{0}{0}-{0}{0}-{0}{0} {0}{0}:{0}{0}:{0}{0}",
                widestDigit);
            string tagAllowance = " [" + new string('w', TagColumnChars) + "]";

            int widest = 0;
            foreach (ModuleLogLevel level in Enum.GetValues(typeof(ModuleLogLevel)))
            {
                string template = "[" + level.ToString().ToUpperInvariant() + "] " + stamp + tagAllowance;
                widest = Math.Max(widest, Measure(font, template).Width);
            }

            _fullPrefixWidth = widest;
            return _fullPrefixWidth;
        }

        private static (int Width, int Height) Measure(BitmapFont font, string text)
        {
            var size = font.MeasureString(text);
            return ((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
        }

        /// <summary>
        /// Builds one entry's row: a dim, level-tinted prefix Label at x=0
        /// and the message Label at the shared message-column x, each with
        /// an explicit width and each ellipsized to it. Shared by both
        /// render paths so a full rebuild and an incremental append can
        /// never produce differently-shaped rows.
        /// </summary>
        private void CreateRow(ModuleLogEntry entry, string line, long absoluteIndex, RowMetrics metrics)
        {
            Color levelColor = ColorForLevel(entry.Level);

            var row = new LogRow
            {
                AbsoluteIndex = absoluteIndex,
                FullPrefix = LogLineFormat.Prefix(entry),
                FullMessage = LogLineFormat.Message(entry),
                FullLine = TooltipTextFormat.Wrap(line)
            };

            row.Panel = new Panel
            {
                // Sized before it is parented so the flow panel does not
                // see a zero-sized child first.
                Size = new Point(metrics.RowWidth, metrics.RowHeight),
                Parent = _contentPanel
            };

            row.PrefixLabel = new Label
            {
                AutoSizeWidth = false,
                AutoSizeHeight = false,
                TextColor = levelColor * PrefixDimFactor,
                Location = new Point(0, 0),
                Parent = row.Panel
            };

            row.MessageLabel = new Label
            {
                AutoSizeWidth = false,
                AutoSizeHeight = false,
                TextColor = levelColor,
                Parent = row.Panel
            };

            ApplyRowLayout(row, metrics);
            _renderedRows.Enqueue(row);
        }

        /// <summary>
        /// (Re)fits one already-built row to the given geometry - the single
        /// place row text and column sizes are assigned, so the initial
        /// build and every resize re-fit cannot diverge.
        /// </summary>
        private static void ApplyRowLayout(LogRow row, RowMetrics metrics)
        {
            // Both texts are resolved BEFORE the columns are resized:
            // KeepsFitting compares the new column width against the width
            // the label is still carrying from the previous pass.
            string prefixText = KeepsFitting(row.PrefixLabel, row.FullPrefix, metrics.PrefixWidth)
                ? row.FullPrefix
                : LabelHelpers.EllipsizeToWidth(metrics.Font, row.FullPrefix, metrics.PrefixWidth);
            string messageText = KeepsFitting(row.MessageLabel, row.FullMessage, metrics.MessageMaxWidth)
                ? row.FullMessage
                : LabelHelpers.EllipsizeToWidth(metrics.Font, row.FullMessage, metrics.MessageMaxWidth);

            row.Panel.Size = new Point(metrics.RowWidth, metrics.RowHeight);
            row.PrefixLabel.Size = new Point(metrics.PrefixWidth, metrics.RowHeight);
            row.MessageLabel.Location = new Point(metrics.MessageX, 0);
            row.MessageLabel.Size = new Point(metrics.MessageMaxWidth, metrics.RowHeight);

            // Assigning Label.Text invalidates layout, so only assign when
            // the displayed string actually changed - the same gate
            // IconNameRowHelpers.ReellipsizeName uses on the plan tab's own
            // resize path.
            if (!string.Equals(row.PrefixLabel.Text, prefixText, StringComparison.Ordinal))
            {
                row.PrefixLabel.Text = prefixText;
            }

            if (!string.Equals(row.MessageLabel.Text, messageText, StringComparison.Ordinal))
            {
                row.MessageLabel.Text = messageText;
            }

            // The tooltip is the only indication that a row is not showing
            // everything, so it carries the WHOLE line (both columns) the
            // moment either one had to shorten - the same "ellipsize +
            // tooltip" contract every truncatable row in this module uses.
            // Blish resolves a tooltip on the control under the mouse and
            // does not bubble to the parent, so both Labels need it as well
            // as the row Panel - the swallowed-hover class already fixed in
            // ShoppingListSectionRenderer (docs/KNOWN-ISSUES.md).
            bool shortened =
                !string.Equals(prefixText, row.FullPrefix, StringComparison.Ordinal) ||
                !string.Equals(messageText, row.FullMessage, StringComparison.Ordinal);
            //
            // FullLine is stored already wrapped (see CreateRow), so this
            // per-render guard still compares like with like and the
            // facility call below only runs when the tooltip actually
            // changed - this method runs for every visible row on every
            // resize and scroll.
            string tooltip = shortened ? row.FullLine : null;
            if (!string.Equals(row.Panel.BasicTooltipText, tooltip, StringComparison.Ordinal))
            {
                TooltipFacility.ApplyPlain(row.Panel, tooltip);
                TooltipFacility.ApplyPlain(row.PrefixLabel, tooltip);
                TooltipFacility.ApplyPlain(row.MessageLabel, tooltip);
            }
        }

        /// <summary>
        /// True when the label already shows the untruncated string and its
        /// column only grew - it cannot have started to overflow, so the
        /// MeasureString binary search inside EllipsizeToWidth can be
        /// skipped. Matters on a horizontal resize drag, which re-fits every
        /// visible row (up to the ring's 2000) on every tick.
        /// </summary>
        private static bool KeepsFitting(Label label, string full, int newWidth)
        {
            return ReferenceEquals(label.Text, full) && newWidth >= label.Width;
        }

        /// <summary>
        /// Re-fits every visible row after a container resize. A
        /// vertical-only drag leaves the content width alone and returns
        /// here without touching a single row.
        /// <para>
        /// Wrapped in SuspendLayout/ResumeLayout for the same reason
        /// CraftingPlanView.ReplayRelayout is (see its doc comment):
        /// assigning a row Panel's Size fires that Panel's own Resized
        /// event, which FlowPanel wires to a full reflow of ALL its
        /// children - so an unsuspended loop over a full ring would cost
        /// O(rows^2) position writes, plus a fresh children array per
        /// reflow, on every frame of a horizontal drag. Suspending the
        /// parent propagates down (Blish's IsLayoutSuspended walks the
        /// parent chain); ResumeLayout(false) leaves the single coalesced
        /// reflow to Blish's own next-frame UpdateLayout rather than
        /// forcing it synchronously here.
        /// </para>
        /// </summary>
        private void RefitRows()
        {
            if (!IsLive || _contentPanel.Width == _lastLayoutWidth)
            {
                return;
            }

            var metrics = MeasureRowMetrics();

            _contentPanel.SuspendLayout();
            try
            {
                foreach (var row in _renderedRows)
                {
                    ApplyRowLayout(row, metrics);
                }
            }
            finally
            {
                _contentPanel.ResumeLayout(false);
            }
        }

        private (List<(ModuleLogEntry Entry, string Line, long AbsoluteIndex)> Filtered, int RawCount, long Version) GetFilteredEntries()
        {
            var entries = _log.Snapshot(out long version);
            long startIndex = version - entries.Count;

            ModuleLogLevel minLevel = MinLevelForFilter();
            string search = _searchBox?.Text?.Trim() ?? string.Empty;

            // Read once rather than per-iteration - see AppendNewRows' own
            // comment on the identical pattern.
            long clearedBeforeVersion = _getClearedBeforeVersion();

            var filtered = new List<(ModuleLogEntry Entry, string Line, long AbsoluteIndex)>();
            for (int i = 0; i < entries.Count; i++)
            {
                long absoluteIndex = startIndex + i;
                if (!LogViewFloor.IsVisible(absoluteIndex, clearedBeforeVersion))
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

            string candidate = LogLineFormat.Line(entry);
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
