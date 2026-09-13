using System;

namespace Mmzkworks.muProperty
{
    /// <summary>
    /// Instance wrappers for <see cref="PropertyTreeMerger"/>.
    /// </summary>
    public static class PropertyTreeExtensions
    {
        public static PropertyTree Merge(this PropertyTree left, PropertyTree right)
        {
            return PropertyTreeMerger.Merge(left, right);
        }

        public static PropertyTree Merge(this PropertyTree left, params PropertyTree[] others)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (others == null)
            {
                throw new ArgumentNullException(nameof(others));
            }

            var acc = left;
            for (var i = 0; i < others.Length; i++)
            {
                acc = PropertyTreeMerger.Merge(acc, others[i]);
            }

            return others.Length == 0 ? left.Clone() : acc;
        }
    }
}
