namespace TaimisToolbench.Services
{
    /// <summary>
    /// Where a table's column-header WORD sits on a column whose rows draw
    /// an icon before their text.
    /// <para>
    /// The icon is that column's own content, not a column of its own, so
    /// the header rules on the gutter's left edge and not on the text
    /// inside it. Seating the word at the text x instead indents it
    /// relative to the column it names, by exactly the gutter's width.
    /// </para>
    /// <para>
    /// This says nothing about where the column BEGINS. A column with a
    /// neighbour before its gutter - the Ranker's rank column, a tree's
    /// caret - keeps that neighbour out of its span, so a caller passes
    /// its own gutter x and never the band's left edge.
    /// </para>
    /// </summary>
    internal static class ColumnHeaderLabelMath
    {
        /// <summary>What a column whose rows draw no icon passes as its
        /// gutter: its header stays on the text rule it already had.</summary>
        public const int NoIconGutter = int.MinValue;

        /// <summary>
        /// x of the header word for a column whose text starts at
        /// <paramref name="textX"/> and whose icon gutter starts at
        /// <paramref name="iconGutterX"/>. A gutter at or right of the text
        /// is not one the column draws through and is ignored: the word can
        /// only ever move LEFT of the rule its own text keeps, never right
        /// of it and so never out of its column.
        /// </summary>
        public static int LabelX(int textX, int iconGutterX)
        {
            if (iconGutterX == NoIconGutter || iconGutterX >= textX)
            {
                return textX;
            }

            return iconGutterX;
        }
    }
}
