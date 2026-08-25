using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Turns a column-header band into a row of CELLS: hovering anywhere in
    /// a sortable column's cell washes it, and clicking anywhere in it
    /// sorts. Field report: <i>"the header rows of columns that you can
    /// click to sort should highlight lightly when you mouse over them...
    /// the tooltip and click action should probably trigger for mouseover
    /// of the entire column header cell, not just the text."</i>
    ///
    /// <para>
    /// The mechanism, measured against decompiled Blish HUD 1.3.0 rather
    /// than assumed: <c>Container.TriggerMouseInput</c> raises the
    /// CONTAINER's own mouse events first and only then walks its children,
    /// and <c>Control.CheckMouseLeft</c> clears MouseOver only when the
    /// cursor leaves that control's own bounds. So the header row panel
    /// sees every move, press, release and click inside the band - INCLUDING
    /// those over its labels - and the cell under the cursor can be decided
    /// from <c>RelativeMousePosition</c>. That is why the wash panels are
    /// passive scenery here and every handler lives on the row.
    /// </para>
    ///
    /// <para>
    /// Two things do not follow from that. Blish resolves a TOOLTIP on the
    /// deepest control under the cursor and never bubbles, so the note is
    /// stamped on the label AND on the cell's own wash - whichever of the
    /// two the cursor is over is the only one that can answer. And the wash
    /// panels carry <c>ZIndex = -1</c>: Blish draws children ordered by
    /// ZIndex, so without it a wash created after its label would paint
    /// over the text.
    /// </para>
    /// </summary>
    internal sealed class SortableHeaderCells
    {
        /// <summary>
        /// The wash. Light enough to read as an affordance rather than a
        /// selection, and it is the ONE property this hover scheme writes
        /// (PressFeedback's Opacity dim composes with it rather than
        /// fighting it - see that class's own note).
        /// </summary>
        private static readonly Color HoverWash = new Color(255, 255, 255) * 0.07f;

        /// <summary>Held: the same wash, doubled. A press has to be visible
        /// on a cell whose label is already tinted.</summary>
        private static readonly Color PressedWash = new Color(255, 255, 255) * 0.14f;

        /// <summary>
        /// Hover tint for the cell's label - the affordance a header had
        /// before it had a cell, kept because an unsorted column shows no
        /// sort indicator and the wash alone is deliberately faint.
        /// </summary>
        private static readonly Color HoverTextColor = new Color(255, 224, 150);

        private sealed class Cell
        {
            internal Panel Wash;
            internal Label Label;
            internal Color RestingTextColor;
            internal Action OnClick;
            internal int X;
            internal int Width;

            /// <summary>Whether the wash already carries the shared note.
            /// Stamping is a wrap and an allocation, and Sync can run on
            /// every frame of a drag (see HeaderCellPlan).</summary>
            internal bool Stamped;

            internal bool Contains(int x)
            {
                return x >= X && x < X + Width;
            }
        }

        /// <summary>One header cell as its caller describes it: where the
        /// column is, which label sits in it, and what a click does.</summary>
        internal readonly struct Column
        {
            internal readonly int X;
            internal readonly int Width;
            internal readonly Label Label;
            internal readonly Action OnClick;

            internal Column(int x, int width, Label label, Action onClick)
            {
                X = x;
                Width = width;
                Label = label;
                OnClick = onClick;
            }
        }

        private readonly Panel _rowPanel;
        private readonly List<Cell> _cells = new List<Cell>();
        private int _hovered = -1;
        private bool _held;

        internal SortableHeaderCells(Panel rowPanel)
        {
            _rowPanel = rowPanel;

            _rowPanel.MouseMoved += (_, __) => SetHovered(IndexAt(_rowPanel.RelativeMousePosition.X));
            _rowPanel.MouseLeft += (_, __) =>
            {
                _held = false;
                SetHovered(-1);
            };
            _rowPanel.LeftMouseButtonPressed += (_, __) =>
            {
                if (_hovered < 0) return;
                _held = true;
                Repaint();
                PressFeedback.PlayClick();
            };
            _rowPanel.LeftMouseButtonReleased += (_, __) =>
            {
                _held = false;
                Repaint();
            };
            _rowPanel.Click += (_, __) =>
            {
                int index = IndexAt(_rowPanel.RelativeMousePosition.X);
                if (index >= 0)
                {
                    _cells[index].OnClick();
                }
            };
        }

        /// <summary>
        /// Re-describes the band's cells - at build time and again from the
        /// section's relayout, because a right-pinned column's x is a
        /// function of the panel width. Cells are reused rather than
        /// rebuilt: on the per-frame callers (see HeaderCellPlan) a drag
        /// would otherwise churn a control per column per frame.
        /// </summary>
        internal void Sync(IReadOnlyList<Column> columns)
        {
            int rowHeight = _rowPanel.Height;

            for (int i = 0; i < columns.Count; i++)
            {
                if (i == _cells.Count)
                {
                    _cells.Add(new Cell
                    {
                        Wash = new Panel()
                        {
                            BackgroundColor = Color.Transparent,
                            // Under the labels: Blish draws children in
                            // ZIndex order, and the wash is scenery.
                            ZIndex = -1,
                            Parent = _rowPanel
                        }
                    });
                }

                var cell = _cells[i];
                cell.Label = columns[i].Label;
                cell.RestingTextColor = TableHeaderStyle.LabelColor;
                cell.OnClick = columns[i].OnClick;
                cell.X = columns[i].X;
                cell.Width = columns[i].Width;
                cell.Wash.Location = new Point(cell.X, 0);
                cell.Wash.Size = new Point(cell.Width, rowHeight);

                // An unsortable column's cell is not built at all: hidden,
                // Blish skips it in both the hit test and the draw, so it
                // neither answers a hover nor covers the band.
                cell.Wash.Visible = cell.OnClick != null;
                if (cell.Wash.Visible && !cell.Stamped)
                {
                    SortableHeaderLabel.MarkSortable(cell.Wash);
                    cell.Stamped = true;
                }
            }

            for (int i = columns.Count; i < _cells.Count; i++)
            {
                _cells[i].Wash.Visible = false;
                _cells[i].Width = 0;
                _cells[i].OnClick = null;
                _cells[i].Label = null;
            }

            // The cell under a stationary cursor may have moved out from
            // under it, or stopped existing.
            if (_hovered >= columns.Count)
            {
                SetHovered(-1);
            }
            else
            {
                Repaint();
            }
        }

        private int IndexAt(int x)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].OnClick != null && _cells[i].Contains(x))
                {
                    return i;
                }
            }

            return -1;
        }

        private void SetHovered(int index)
        {
            if (index == _hovered) return;

            _hovered = index;
            _held = false;
            Repaint();
        }

        private void Repaint()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                bool active = i == _hovered;
                cell.Wash.BackgroundColor = active
                    ? (_held ? PressedWash : HoverWash)
                    : Color.Transparent;

                if (cell.Label != null)
                {
                    cell.Label.TextColor = active ? HoverTextColor : cell.RestingTextColor;
                }
            }
        }
    }
}
