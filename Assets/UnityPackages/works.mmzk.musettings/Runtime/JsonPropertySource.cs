using System;
using System.IO;
using Mmzkworks.muProperty;

namespace Mmzkworks.muSettings
{
    /// <summary>
    /// Loads and saves a nested JSON object as a <see cref="PropertyTree"/>.
    /// </summary>
    public sealed class JsonPropertySource : IPropertyTreeSource
    {
        readonly string _filePath;

        public JsonPropertySource(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path must not be empty.", nameof(filePath));
            }

            _filePath = filePath;
        }

        public PropertyTree Load()
        {
            var json = File.ReadAllText(_filePath);
            return Parse(json);
        }

        public void Save(PropertyTree tree)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, ToJson(tree));
        }

        public static PropertyTree Parse(string json)
        {
            return JsonPropertyTreeCodec.Parse(json);
        }

        public static string ToJson(PropertyTree tree)
        {
            return JsonPropertyTreeCodec.ToJson(tree);
        }
    }
}
