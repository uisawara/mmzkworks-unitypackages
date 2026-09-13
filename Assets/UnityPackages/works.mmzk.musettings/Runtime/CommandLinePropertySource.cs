using System;
using Mmzkworks.muProperty;

namespace Mmzkworks.muSettings
{
    /// <summary>
    /// Reads <c>--key=value</c>, <c>--key value</c>, and slash-path single-dash options into a tree.
    /// </summary>
    public sealed class CommandLinePropertySource : IPropertyTreeSource
    {
        readonly string[] _args;

        public CommandLinePropertySource()
            : this(Environment.GetCommandLineArgs())
        {
        }

        public CommandLinePropertySource(string[] args)
        {
            _args = args ?? throw new ArgumentNullException(nameof(args));
        }

        public PropertyTree Load()
        {
            var tree = new PropertyTree();
            for (var i = 0; i < _args.Length; i++)
            {
                var arg = _args[i];
                if (string.IsNullOrEmpty(arg) || arg == "--" || arg == "-")
                {
                    continue;
                }

                string key;
                var explicitValue = false;
                string value = null;

                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    var body = arg.Substring(2);
                    SplitKeyValue(body, out key, out value, out explicitValue);
                }
                else if (arg[0] == '-' && arg.IndexOf('/') >= 0)
                {
                    var body = arg.Substring(1);
                    SplitKeyValue(body, out key, out value, out explicitValue);
                }
                else
                {
                    continue;
                }

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!explicitValue)
                {
                    if (i + 1 < _args.Length && !IsOption(_args[i + 1]))
                    {
                        value = _args[++i];
                    }
                    else
                    {
                        value = "true";
                    }
                }

                tree.Set(key, PropertyValueParser.Parse(value));
            }

            return tree;
        }

        static void SplitKeyValue(string body, out string key, out string value, out bool explicitValue)
        {
            var eq = body.IndexOf('=');
            if (eq >= 0)
            {
                key = body.Substring(0, eq);
                value = body.Substring(eq + 1);
                explicitValue = true;
                return;
            }

            key = body;
            value = null;
            explicitValue = false;
        }

        static bool IsOption(string arg)
        {
            return !string.IsNullOrEmpty(arg) && arg[0] == '-';
        }
    }
}
