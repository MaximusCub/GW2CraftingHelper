using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Views
{
    public class ItemSelectedEventArgs : EventArgs
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
    }

    public class SuggestionPanel : IDisposable
    {
        private const int MaxResults = 8;
        private const int RowHeight = 28;
        private const int IconSize = 24;
        private const int IconPad = 4;

        private readonly AutocompleteTextBox _textBox;
        private readonly IItemSearchProvider _searchProvider;

        private Panel _panel;
        private FlowPanel _rowContainer;
        private IReadOnlyList<ItemSearchResult> _results = Array.Empty<ItemSearchResult>();
        private int _highlightIndex;
        private bool _disposed;
        private bool _suppressTextChanged;
        private bool _globalMouseHooked;
        private CancellationTokenSource _searchCts;

        public event EventHandler<ItemSelectedEventArgs> ItemSelected;

        public SuggestionPanel(
            AutocompleteTextBox textBox,
            IItemSearchProvider searchProvider)
        {
            _textBox = textBox;
            _searchProvider = searchProvider;

            _textBox.TextChanged += OnTextChanged;
            _textBox.ArrowPressed += OnArrowPressed;
            _textBox.EnterKeyPressed += OnEnterPressed;
            _textBox.InputFocusChanged += OnFocusChanged;
            _textBox.Moved += OnTextBoxMoved;
        }

        private async void OnTextChanged(object sender, EventArgs e)
        {
            if (_suppressTextChanged || _disposed) return;

            string text = _textBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                HidePanel();
                return;
            }

            string query = text.Trim();

            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            IReadOnlyList<ItemSearchResult> results;
            try
            {
                results = await _searchProvider.SearchAsync(query, MaxResults, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                return;
            }

            // Blish HUD's XNA host has no SynchronizationContext, so this
            // continuation may resume on a ThreadPool thread if the search
            // provider ever completes asynchronously (see
            // Contracts/IItemSearchProvider.cs). Both providers wired up
            // today return already-completed tasks, so this is currently
            // inert, but marshal pre-emptively so a future provider cannot
            // reintroduce an off-thread control mutation here. The
            // stale-query guard stays inside the marshaled action so stale
            // results are still discarded against current state at the
            // moment of UI application, not at the moment the search
            // finished. The focus check guards against a slower path: the
            // textbox can lose focus (OnFocusChanged hides the panel) while
            // this search is still in flight, and without re-checking focus
            // here a queued result could ShowPanel() again right after
            // dismissal.
            MainThreadMarshal.Run(() =>
            {
                if (ct.IsCancellationRequested || _disposed || !_textBox.Focused) return;
                if (_textBox.Text == null || _textBox.Text.Trim() != query) return;

                if (results == null || results.Count == 0)
                {
                    HidePanel();
                    return;
                }

                _results = results;
                _highlightIndex = 0;
                RebuildRows();
                ShowPanel();
            });
        }

        private void OnArrowPressed(object sender, int delta)
        {
            if (_disposed || _results.Count == 0 || _panel == null || !_panel.Visible) return;

            _highlightIndex += delta;

            // Wrap around
            if (_highlightIndex < 0) _highlightIndex = _results.Count - 1;
            if (_highlightIndex >= _results.Count) _highlightIndex = 0;

            UpdateHighlights();
        }

        private void OnEnterPressed(object sender, AutocompleteEnterEventArgs e)
        {
            if (_disposed) return;

            if (_panel != null && _panel.Visible && _results.Count > 0)
            {
                e.Handled = true;
                SelectItem(_highlightIndex);
            }
        }

        private void OnFocusChanged(object sender, EventArgs e)
        {
            if (_disposed) return;

            bool hasFocus = _textBox.Focused;
            if (!hasFocus)
            {
                // If mouse is over the panel, re-focus the textbox
                // so the click on the row can fire before we dismiss
                if (_panel != null && _panel.MouseOver)
                {
                    _textBox.Focused = true;
                    return;
                }

                HidePanel();
            }
        }

        private void OnTextBoxMoved(object sender, MovedEventArgs e)
        {
            if (_panel != null && _panel.Visible)
            {
                PositionPanel();
            }
        }

        private void EnsurePanel()
        {
            if (_panel != null) return;

            _panel = new Panel()
            {
                Parent = GameService.Graphics.SpriteScreen,
                ZIndex = Screen.TOOLTIP_BASEZINDEX,
                BackgroundColor = new Color(30, 30, 30, 240),
                Visible = false
            };

            _rowContainer = new FlowPanel()
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                Parent = _panel
            };
        }

        private void RebuildRows()
        {
            EnsurePanel();

            // Clear old rows
            foreach (var child in _rowContainer.Children.ToArray())
            {
                child.Dispose();
            }

            int panelWidth = _textBox.Width;

            for (int i = 0; i < _results.Count; i++)
            {
                int index = i;
                var item = _results[i];

                var row = new Panel()
                {
                    Size = new Point(panelWidth, RowHeight),
                    BackgroundColor = i == _highlightIndex
                        ? new Color(60, 60, 60, 255)
                        : new Color(30, 30, 30, 240),
                    Parent = _rowContainer
                };

                // Item icon. A missing IconUrl is a data gap, never a
                // genuine load failure - IconControls.CreateItemIcon
                // degrades it to a neutral empty-slot square instead of
                // Blish's alarming magenta missing-texture placeholder
                // (audit row 56 PART B #1, sibling of the same fix in
                // MainView.cs's snapshot/wallet rows).
                IconControls.CreateItemIcon(row, item.IconUrl, 2, (RowHeight - IconSize) / 2, IconSize);

                // Item name
                new Label()
                {
                    Font = UiFonts.Body,
                    Text = item.Name,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(2 + IconSize + IconPad, (RowHeight - 16) / 2),
                    Parent = row
                };

                row.MouseEntered += (_, __) =>
                {
                    _highlightIndex = index;
                    UpdateHighlights();
                };

                PressFeedback.Wire(row);

                row.Click += (_, __) =>
                {
                    SelectItem(index);
                };
            }

            int totalHeight = _results.Count * RowHeight;
            _panel.Size = new Point(panelWidth, totalHeight);
            _rowContainer.Size = new Point(panelWidth, totalHeight);

            PositionPanel();
        }

        private void UpdateHighlights()
        {
            var children = _rowContainer.Children.ToArray();
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i] as Panel;
                if (child != null)
                {
                    child.BackgroundColor = i == _highlightIndex
                        ? new Color(60, 60, 60, 255)
                        : new Color(30, 30, 30, 240);
                }
            }
        }

        private void PositionPanel()
        {
            if (_panel == null) return;

            var tbBounds = _textBox.AbsoluteBounds;
            int panelHeight = _panel.Height;
            var screen = GameService.Graphics.SpriteScreen;

            int yBelow = (int)tbBounds.Bottom;
            bool fitBelow = (yBelow + panelHeight) <= screen.Height;
            int y = fitBelow ? yBelow : Math.Max(0, (int)tbBounds.Top - panelHeight);

            // Left edge of the text box: a classic dropdown, directly under
            // the box being typed into. It was anchored right of the Qty
            // stepper for a while so it would not cover the controls beneath
            // it, and the field test rejected that outright - "the typeahead
            // popup ... floats far off to the right". Transiently covering
            // the row's own quantity field and the rows below is what a
            // dropdown does; it closes on pick, on the box losing focus and
            // on a click outside.
            //
            // Still held on screen: the box belongs to a window the user may
            // have dragged against the right edge.
            int x = (int)tbBounds.X;
            int maxX = Math.Max(0, screen.Width - _panel.Width);
            if (x > maxX)
            {
                x = maxX;
            }

            _panel.Location = new Point(x, y);
        }

        private void ShowPanel()
        {
            if (_panel == null) return;

            _panel.Visible = true;

            if (!_globalMouseHooked)
            {
                GameService.Input.Mouse.LeftMouseButtonPressed += OnGlobalMouseClick;
                _globalMouseHooked = true;
            }
        }

        public void HidePanel()
        {
            if (_panel != null)
            {
                _panel.Visible = false;
            }

            if (_globalMouseHooked)
            {
                GameService.Input.Mouse.LeftMouseButtonPressed -= OnGlobalMouseClick;
                _globalMouseHooked = false;
            }
        }

        private void OnGlobalMouseClick(object sender, MouseEventArgs e)
        {
            if (_disposed) return;

            if (_panel != null && !_panel.MouseOver && !_textBox.MouseOver)
            {
                HidePanel();
            }
        }

        private void SelectItem(int index)
        {
            if (index < 0 || index >= _results.Count) return;

            var item = _results[index];

            HidePanel();

            // Set text without triggering a new search
            _suppressTextChanged = true;
            _textBox.Text = item.Name;
            _suppressTextChanged = false;

            ItemSelected?.Invoke(this, new ItemSelectedEventArgs
            {
                ItemId = item.ItemId,
                Name = item.Name,
                IconUrl = item.IconUrl
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _textBox.TextChanged -= OnTextChanged;
            _textBox.ArrowPressed -= OnArrowPressed;
            _textBox.EnterKeyPressed -= OnEnterPressed;
            _textBox.InputFocusChanged -= OnFocusChanged;
            _textBox.Moved -= OnTextBoxMoved;

            if (_globalMouseHooked)
            {
                GameService.Input.Mouse.LeftMouseButtonPressed -= OnGlobalMouseClick;
                _globalMouseHooked = false;
            }

            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;

            _panel?.Dispose();
            _panel = null;
            _rowContainer = null;
        }
    }
}
