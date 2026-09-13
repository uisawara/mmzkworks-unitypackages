using Mmzkworks.muProperty;
using NUnit.Framework;

namespace Mmzkworks.muSettings.Tests
{
    public class PropertyTreeLoaderTests
    {
        [Test]
        public void Load_MergesLeftToRight()
        {
            var defaults = new PropertyTree();
            defaults.Set("a", 1);
            defaults.Set("b", 2);
            var overlay = new PropertyTree();
            overlay.Set("b", 9);
            overlay.Set("c", 3);

            var merged = PropertyTreeLoader.Load(
                new StaticSource(defaults),
                new StaticSource(overlay));

            Assert.AreEqual(1, merged.GetInt("a"));
            Assert.AreEqual(9, merged.GetInt("b"));
            Assert.AreEqual(3, merged.GetInt("c"));
        }

        [Test]
        public void Load_EmptySources_ReturnsEmptyTree()
        {
            var tree = PropertyTreeLoader.Load();
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Load_CliOverridesJson()
        {
            var json = JsonPropertySource.Parse(@"{ ""render"": { ""quality"": 1, ""name"": ""json"" } }");
            var cli = new CommandLinePropertySource(new[] { "--render/quality=2" });
            var merged = PropertyTreeLoader.Load(new StaticSource(json), cli);
            Assert.AreEqual(2, merged.GetInt("render/quality"));
            Assert.AreEqual("json", merged.GetString("render/name"));
        }

        sealed class StaticSource : IPropertyTreeSource
        {
            readonly PropertyTree _tree;

            public StaticSource(PropertyTree tree)
            {
                _tree = tree;
            }

            public PropertyTree Load()
            {
                return _tree.Clone();
            }
        }
    }
}
