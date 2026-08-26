using Blish_HUD.Controls;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// The multi-item request editor at the top of the Crafting Plan tab
    /// (gw2efficiency parity): the session-persistent row list, the live
    /// Blish controls each row owns, and the +/- buttons that grow and
    /// shrink it.
    /// <para>
    /// Lives in <c>Views/</c> rather than <c>Views/Rendering/</c> because
    /// its controls are <c>AutocompleteTextBox</c>, <c>SuggestionPanel</c>
    /// and <c>FocusRelease</c>, all of which are <c>Views</c> types -
    /// putting the strip under <c>Views/Rendering</c> would make that
    /// folder reference <c>Views</c> and reverse the one-way dependency
    /// docs/ARCHITECTURE.md section 5 states.
    /// </para>
    /// <para>
    /// The strip owns row state and row controls and nothing else. It does
    /// not own the panel it draws into (<c>CraftingPlanView</c> creates and
    /// sizes that as part of the top strip's Y arithmetic) and it does not
    /// own the reflow a row count change triggers: adding or removing a row
    /// changes the whole top region's height, which is
    /// <c>ReflowTopRegion</c>'s job, so the strip raises
    /// <c>onRowCountChanged</c> and lets the view decide what moves.
    /// </para>
    /// </summary>
    internal sealed class ItemInputRowStrip
    {
        // Item-row geometry, left to right: search box, "Qty:" label,
        // quantity field, then the add/remove buttons. The buttons keep a
        // clear gap from the quantity field so "+" does not read as its
        // stepper.
        private const int QtyInputX = 240;
        private const int QtyInputWidth = 50;
        private const int RowButtonsX = 320;

        // The row's +/- pair: square, and the same height as every other
        // button in the module - which is also the height of the search and
        // quantity boxes they sit beside, so the run now shares one baseline
        // instead of mixing 28px inputs with 24px buttons.
        private const int RowButtonSize = UiMetrics.ButtonHeight;
        private const int RowButtonGap = 8;
        private const int RowButtonY = 3;

        // The strip's rows are the top region's rows: read straight from
        // TopRegionLayoutMath rather than re-aliased here, so a row panel
        // can never disagree with the height the strip was laid out for.
        private const int RowHeight = TopRegionLayoutMath.TopRegionRowHeight;

        // Session-persistent row list, mirroring gw2e's `e.recipes`
        // array. Populated with one empty row on the first Build();
        // survives every later Build() (tab switch). The rows themselves
        // are never written to disk - a restored plan reseeds them from
        // its persisted request via RestoreRows.
        private readonly List<ItemRowState> _rows = new List<ItemRowState>();

        private readonly IItemSearchProvider _itemSearchProvider;
        private readonly Action _onRowCountChanged;

        // The panel the rows are parented into, re-supplied by every
        // Rebuild: a Build() hands over a brand new one, a same-cycle
        // add/remove hands back the live one.
        private Panel _inputPanel;

        internal ItemInputRowStrip(IItemSearchProvider itemSearchProvider, Action onRowCountChanged)
        {
            _itemSearchProvider = itemSearchProvider;
            _onRowCountChanged = onRowCountChanged ?? throw new ArgumentNullException(nameof(onRowCountChanged));
        }

        /// <summary>
        /// The live rows, for the callers that read a row's selection or
        /// quantity (the top strip's own height arithmetic, and Generate's
        /// typed-name resolution). Read-only: rows are added and removed
        /// only through this class, so a row can never appear without the
        /// controls and the reflow that go with it.
        /// </summary>
        internal IReadOnlyList<ItemRowState> Rows => _rows;

        /// <summary>
        /// Gw2e's own initial state is one empty row
        /// (`e.recipes = [{id: null, amount: 1}]`) - see ItemRowState's own
        /// doc comment. Only ever seeded once; every later Build() call
        /// (tab switch) reuses whatever the session already has.
        /// </summary>
        internal void SeedFirstRow()
        {
            if (_rows.Count == 0)
            {
                _rows.Add(new ItemRowState());
            }
        }

        /// <summary>
        /// Replaces the session row list with the request a restored plan
        /// was generated for, so a restored session's Generate Plan
        /// re-solves the same request with zero retyping. A named seed
        /// leaves exactly the state a suggestion pick leaves behind
        /// (TypedText mirroring ItemName - see the pick's own TextChanged
        /// ordering in CreateItemRowControls); an unnamed one keeps the id
        /// with no text, which SelectionIsStale treats as still-resolved.
        /// No-op on an empty seed list so a plan persisted without request
        /// items cannot wipe the strip's default row.
        /// <para>
        /// Any live row controls are disposed first, RemoveItemRow-style
        /// (with Rebuild's own Parent guard against a double-Dispose across
        /// a tab-switch teardown); the row-count callback then rebuilds
        /// controls and reflows the top region. On the usual pre-Build()
        /// restore both halves are no-ops - every control is null and
        /// ReflowTopRegion bails - and Build() itself renders the new rows.
        /// </para>
        /// </summary>
        internal void RestoreRows(IReadOnlyList<RestoredRequestInputs.RowSeed> seeds)
        {
            if (seeds == null || seeds.Count == 0)
            {
                return;
            }

            foreach (var row in _rows)
            {
                row.SuggestionPanel?.Dispose();
                if (row.RowPanel != null && row.RowPanel.Parent != null)
                {
                    row.RowPanel.Dispose();
                }
            }

            _rows.Clear();
            foreach (var seed in seeds)
            {
                _rows.Add(new ItemRowState
                {
                    ItemId = seed.ItemId,
                    ItemName = seed.ItemName,
                    TypedText = seed.ItemName,
                    QuantityText = seed.QuantityText,
                });
            }

            _onRowCountChanged();
        }

        /// <summary>
        /// Disposes every current item row's live controls and rebuilds
        /// them from _rows.
        /// Called by Build() (initial construction) and by
        /// AddItemRow/RemoveItemRow via the row-count-changed callback
        /// (CraftingPlanView.ReflowTopRegion) - a full rebuild rather than
        /// a patch, matching the view's existing dispose+recreate pattern
        /// (e.g. RenderPlan disposes all of _contentPanel's children on
        /// every render rather than diffing). N is always small (a handful
        /// of rows at most), so this is not a hot path.
        /// </summary>
        internal void Rebuild(Panel inputPanel, int w)
        {
            _inputPanel = inputPanel;

            foreach (var row in _rows)
            {
                // SuggestionPanel is SpriteScreen-parented (never a child of
                // _inputPanel/buildPanel), so it always needs an explicit
                // Dispose() regardless of which cycle this is - same
                // reasoning the old single-_suggestionPanel field's Build()
                // cleanup always had. SuggestionPanel.Dispose() itself is
                // idempotent (`if (_disposed) return;`), so this is safe to
                // call even on a row whose SuggestionPanel was already
                // disposed by a previous rebuild this same Build() cycle.
                row.SuggestionPanel?.Dispose();

                // RowPanel, by contrast, IS a child of _inputPanel/buildPanel
                // - across a tab-switch Build() cycle it (and its own
                // children) were already torn down by ViewAdapter's own
                // "clear existing children before rebuilding" cascade before
                // this method ever runs again, which nulls a disposed
                // control's Parent (see TriggerGenerate's own "a disposed
                // control's Parent is nulled on disposal" comment). Disposing
                // it again here would be a double-Dispose on an
                // already-torn-down control; only a genuine same-cycle
                // Add/Remove reflow (ReflowTopRegion, _inputPanel still
                // live) leaves RowPanel.Parent non-null, meaning THIS row
                // genuinely still needs disposing before its replacement is
                // built.
                if (row.RowPanel != null && row.RowPanel.Parent != null)
                {
                    row.RowPanel.Dispose();
                }

                row.SuggestionPanel = null;
                row.RowPanel = null;
                row.SearchBox = null;
                row.QtyInput = null;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                CreateItemRowControls(_rows[i], i, w);
            }
        }

        /// <summary>
        /// Widths only, for a resize that did not change the row count -
        /// the counterpart of <see cref="Rebuild"/>, which a drag must
        /// never reach (it would tear down the user's in-progress typing
        /// and their open suggestion list on every tick).
        /// </summary>
        internal void ResizeRows(int w)
        {
            foreach (var row in _rows)
            {
                if (row.RowPanel != null)
                {
                    row.RowPanel.Size = new Point(w, RowHeight);
                }
            }
        }

        /// <summary>
        /// The per-row suggestion popups are SpriteScreen-parented, like the
        /// tickers, so disposing the host window does not reach them and
        /// nothing else tears them down on unload - and each one holds a
        /// global mouse subscription for its whole life. Called by
        /// Module.Unload; every in-session teardown routes through
        /// <see cref="Rebuild"/> instead.
        /// </summary>
        internal void DisposeSuggestionPanels()
        {
            foreach (var row in _rows)
            {
                row.SuggestionPanel?.Dispose();
                row.SuggestionPanel = null;
            }
        }

        /// <summary>
        /// One input row's controls: search box + qty, a Remove button
        /// (gw2e's own 2+-rows gate), and on the last row only an Add
        /// button - attached to the last row rather than its own strip
        /// row so the single-row case keeps the exact original layout.
        /// </summary>
        private void CreateItemRowControls(ItemRowState row, int index, int w)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(w, RowHeight),
                Location = new Point(0, index * RowHeight),
                Parent = _inputPanel,
            };
            row.RowPanel = rowPanel;

            string text = row.TypedText ?? row.ItemName ?? "";
            var searchBox = new AutocompleteTextBox()
            {
                // A restored row can be resolved but nameless (its name
                // was absent from the restored metadata - see
                // RestoredRequestInputs). It still solves; the placeholder
                // just must not claim the box is an empty search, and must
                // never surface the internal item id.
                PlaceholderText = row.ItemId.HasValue && text.Length == 0
                    ? RestoredRequestInputs.UnnamedRowPlaceholder
                    : "Search items...",
                Text = text,
                Size = new Point(200, 28),
                Location = new Point(0, 3),
                Parent = rowPanel,
            }.ReleaseOnDispose().ReleaseOnEnter();
            row.SearchBox = searchBox;

            // The list drops straight under this box (see
            // SuggestionPanel.PositionPanel).
            var suggestionPanel = new SuggestionPanel(searchBox, _itemSearchProvider);
            suggestionPanel.ItemSelected += (_, args) =>
            {
                row.ItemId = args.ItemId;
                row.ItemName = args.Name;
            };
            row.SuggestionPanel = suggestionPanel;

            // A pick is the only thing that resolves a row, so editing the
            // box afterwards has to drop that resolution - otherwise the
            // box reads one item while Generate still plans the previously
            // picked one. Subscribed after SuggestionPanel so a pick's own
            // Text write clears here first and is re-resolved by the
            // ItemSelected handler above, in that order.
            searchBox.TextChanged += (_, __) =>
            {
                row.TypedText = searchBox.Text;

                if (!ItemRowSelection.SelectionIsStale(row.ItemId, row.ItemName, searchBox.Text))
                {
                    return;
                }

                row.ItemId = null;
                row.ItemName = null;
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = "Qty:",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(210, 7),
                Parent = rowPanel,
            };

            var qtyInput = new TextBox()
            {
                Text = string.IsNullOrEmpty(row.QuantityText) ? "1" : row.QuantityText,
                Size = new Point(QtyInputWidth, 28),
                Location = new Point(QtyInputX, 3),
                Parent = rowPanel,
            }.ReleaseOnDispose().ReleaseOnEnter();
            qtyInput.TextChanged += (_, __) => row.QuantityText = qtyInput.Text;
            row.QtyInput = qtyInput;

            int nextX = RowButtonsX;
            if (ItemRowRequestBuilder.CanRemoveRow(_rows.Count))
            {
                var removeButton = new FeedbackButton()
                {
                    Text = "-",
                    Size = new Point(RowButtonSize, RowButtonSize),
                    Location = new Point(nextX, RowButtonY),
                    Parent = rowPanel,
                    BasicTooltipText = "Remove this item from the plan",
                };
                removeButton.Click += (_, __) => RemoveItemRow(row);
                nextX += RowButtonSize + RowButtonGap;
            }

            if (index == _rows.Count - 1)
            {
                var addButton = new FeedbackButton()
                {
                    Text = "+",
                    Size = new Point(RowButtonSize, RowButtonSize),
                    Location = new Point(nextX, RowButtonY),
                    Parent = rowPanel,
                    // Sitting next to the quantity field, a bare "+" reads
                    // as a stepper. Say what it actually adds.
                    BasicTooltipText = "Add another item to this plan",
                };
                addButton.Click += (_, __) => AddItemRow();
            }
        }

        private void AddItemRow()
        {
            _rows.Add(new ItemRowState());
            _onRowCountChanged();
        }

        private void RemoveItemRow(ItemRowState row)
        {
            if (!ItemRowRequestBuilder.CanRemoveRow(_rows.Count))
            {
                return;
            }

            int index = _rows.IndexOf(row);
            if (index < 0)
            {
                return;
            }

            row.SuggestionPanel?.Dispose();
            row.RowPanel?.Dispose();
            _rows.RemoveAt(index);
            _onRowCountChanged();
        }
    }
}
