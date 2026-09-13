using System;
using Mmzkworks.muProperty;

namespace Mmzkworks.muSettings
{
    /// <summary>
    /// Loads sources in order and merges them left-to-right (later sources overwrite).
    /// </summary>
    public static class PropertyTreeLoader
    {
        public static PropertyTree Load(params IPropertyTreeSource[] sources)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            var acc = new PropertyTree();
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source == null)
                {
                    throw new ArgumentNullException(nameof(sources));
                }

                acc = acc.Merge(source.Load());
            }

            return acc;
        }
    }
}
