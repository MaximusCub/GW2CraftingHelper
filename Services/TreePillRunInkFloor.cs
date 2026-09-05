using System.Collections.Generic;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Keeps a recipe-tree row's IGNORE key from MOVING while one plan is
    /// on screen (Blish-free, unit-testable) - the twin of
    /// TreeCostColumnFloor for one row's own pill run.
    /// <para>
    /// TreeIgnoreKeyPlacement seats the key against the run beside it, and
    /// clicking the key changes which pills the row HAS: an ignored node
    /// re-solves to an owned one, so its source pills are gone on the next
    /// render (Services/TreePillRunLayout). Seated against the live run,
    /// the key moved left out from under the cursor that had just clicked
    /// it, and the next click reached the row and expanded the node.
    /// </para>
    /// <para>
    /// So a row's run may widen this floor and never narrow it, and the
    /// floor belongs to the PLAN: a fresh Generate starts again from
    /// nothing. A row toggled back and forth therefore settles on one x
    /// rather than alternating between two.
    /// </para>
    /// </summary>
    internal sealed class TreePillRunInkFloor
    {
        private readonly Dictionary<int, int> _byNodeId = new Dictionary<int, int>();

        /// <summary>
        /// The widest run row <paramref name="nodeId"/> has reported, this
        /// one included. <paramref name="runInk"/> is measured from the
        /// pill column's own left rule rather than in panel coordinates,
        /// so a resize neither widens nor invalidates it.
        /// </summary>
        public int Widen(int nodeId, int runInk)
        {
            if (_byNodeId.TryGetValue(nodeId, out int widest) && widest >= runInk)
            {
                return widest;
            }

            _byNodeId[nodeId] = runInk;
            return runInk;
        }

        /// <summary>Forgets every row, for a genuinely new plan.</summary>
        public void Clear()
        {
            _byNodeId.Clear();
        }
    }
}
