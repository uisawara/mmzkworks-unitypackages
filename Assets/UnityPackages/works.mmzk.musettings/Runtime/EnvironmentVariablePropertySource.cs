using System;
using System.Collections;
using Mmzkworks.muProperty;

namespace Mmzkworks.muSettings
{
    /// <summary>
    /// Reads environment variables. With a prefix, matching names are imported after the prefix is stripped.
    /// Without a prefix, only names that contain <c>/</c> are imported.
    /// </summary>
    public sealed class EnvironmentVariablePropertySource : IPropertyTreeSource
    {
        readonly string _prefix;

        public EnvironmentVariablePropertySource(string prefix = null)
        {
            _prefix = prefix ?? string.Empty;
        }

        public PropertyTree Load()
        {
            var tree = new PropertyTree();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                var name = entry.Key as string;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                string path;
                if (_prefix.Length > 0)
                {
                    if (!name.StartsWith(_prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    path = name.Substring(_prefix.Length);
                    if (path.Length == 0)
                    {
                        continue;
                    }
                }
                else
                {
                    if (name.IndexOf('/') < 0)
                    {
                        continue;
                    }

                    path = name;
                }

                var value = entry.Value as string ?? string.Empty;
                tree.Set(path, PropertyValueParser.Parse(value));
            }

            return tree;
        }
    }
}
