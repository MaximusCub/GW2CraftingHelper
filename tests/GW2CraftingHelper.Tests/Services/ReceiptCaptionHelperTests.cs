using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class ReceiptCaptionHelperTests
    {
        private static CraftingTreeNode Leaf(bool isCostComponent)
        {
            return new CraftingTreeNode { IsCostComponent = isCostComponent };
        }

        // --- ComputeCaptionSplitIndex ---
        [Fact]
        public void ComputeCaptionSplitIndex_StackedCase_ReturnsFirstNonComponentIndex()
        {
            var node = new CraftingTreeNode
            {
                IsReferenceBranch = true,
                Children = new List<CraftingTreeNode> { Leaf(true), Leaf(true), Leaf(false), Leaf(false) },
            };

            Assert.Equal(2, ReceiptCaptionHelper.ComputeCaptionSplitIndex(node));
        }

        [Fact]
        public void ComputeCaptionSplitIndex_SingleComponentLeafThenReference_ReturnsOne()
        {
            var node = new CraftingTreeNode
            {
                IsReferenceBranch = true,
                Children = new List<CraftingTreeNode> { Leaf(true), Leaf(false) },
            };

            Assert.Equal(1, ReceiptCaptionHelper.ComputeCaptionSplitIndex(node));
        }

        [Fact]
        public void ComputeCaptionSplitIndex_NotReferenceBranch_ReturnsMinusOne()
        {
            var node = new CraftingTreeNode
            {
                IsReferenceBranch = false,
                Children = new List<CraftingTreeNode> { Leaf(true), Leaf(false) },
            };

            Assert.Equal(-1, ReceiptCaptionHelper.ComputeCaptionSplitIndex(node));
        }

        [Fact]
        public void ComputeCaptionSplitIndex_FirstChildNotComponent_ReturnsMinusOne()
        {
            // Ordinary reference-branch-only node: children are plain
            // ingredients, never cost components.
            var node = new CraftingTreeNode
            {
                IsReferenceBranch = true,
                Children = new List<CraftingTreeNode> { Leaf(false), Leaf(false) },
            };

            Assert.Equal(-1, ReceiptCaptionHelper.ComputeCaptionSplitIndex(node));
        }

        [Fact]
        public void ComputeCaptionSplitIndex_AllComponentLeaves_NoReferenceGroup_ReturnsMinusOne()
        {
            // Component leaves synthesized but no reference-branch children
            // actually appended (defensive - unreachable via the real
            // builder today, see the helper's own doc comment).
            var node = new CraftingTreeNode
            {
                IsReferenceBranch = true,
                Children = new List<CraftingTreeNode> { Leaf(true), Leaf(true) },
            };

            Assert.Equal(-1, ReceiptCaptionHelper.ComputeCaptionSplitIndex(node));
        }

        [Fact]
        public void ComputeCaptionSplitIndex_NoChildren_ReturnsMinusOne()
        {
            var node = new CraftingTreeNode { IsReferenceBranch = true };

            Assert.Equal(-1, ReceiptCaptionHelper.ComputeCaptionSplitIndex(node));
        }

        [Fact]
        public void ComputeCaptionSplitIndex_NullNode_ReturnsMinusOne()
        {
            Assert.Equal(-1, ReceiptCaptionHelper.ComputeCaptionSplitIndex(null));
        }

        // --- CaptionForChildIndex ---
        [Fact]
        public void CaptionForChildIndex_IndexZero_VendorPriceCaption()
        {
            Assert.Equal(ReceiptCaptionHelper.VendorPriceCaption, ReceiptCaptionHelper.CaptionForChildIndex(2, 0));
        }

        [Fact]
        public void CaptionForChildIndex_SplitIndex_CraftReferenceCaption()
        {
            Assert.Equal(ReceiptCaptionHelper.CraftReferenceCaption, ReceiptCaptionHelper.CaptionForChildIndex(2, 2));
        }

        [Fact]
        public void CaptionForChildIndex_OtherIndex_Null()
        {
            Assert.Null(ReceiptCaptionHelper.CaptionForChildIndex(2, 1));
            Assert.Null(ReceiptCaptionHelper.CaptionForChildIndex(2, 3));
        }

        [Fact]
        public void CaptionForChildIndex_NoSplit_AlwaysNull()
        {
            Assert.Null(ReceiptCaptionHelper.CaptionForChildIndex(-1, 0));
            Assert.Null(ReceiptCaptionHelper.CaptionForChildIndex(-1, 1));
        }
    }
}
