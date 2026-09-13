using System;

namespace Mmzkworks.muProperty
{
    /// <summary>
    /// Left-associative deep merge: left is the base, right overwrites.
    /// Nested objects are merged recursively; a leaf replaces the node at that key.
    /// </summary>
    public static class PropertyTreeMerger
    {
        public static PropertyTree Merge(PropertyTree left, PropertyTree right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            var result = left.Clone();
            result.Overlay(right);
            return result;
        }

        public static PropertyTree Merge(params PropertyTree[] trees)
        {
            if (trees == null)
            {
                throw new ArgumentNullException(nameof(trees));
            }

            if (trees.Length == 0)
            {
                return new PropertyTree();
            }

            PropertyTree acc = null;
            for (var i = 0; i < trees.Length; i++)
            {
                var tree = trees[i];
                if (tree == null)
                {
                    throw new ArgumentNullException(nameof(trees));
                }

                acc = acc == null ? tree.Clone() : Merge(acc, tree);
            }

            return acc;
        }
    }
}
