using NUnit.Framework;

namespace Mmzkworks.muSettings.Tests
{
    public class CommandLinePropertySourceTests
    {
        [Test]
        public void Load_EqualsAndSpaceSeparated()
        {
            var source = new CommandLinePropertySource(new[]
            {
                "Unity",
                "-batchmode",
                "--render/quality=2",
                "--name",
                "hero",
                "-audio/volume",
                "0.5"
            });
            var tree = source.Load();
            Assert.AreEqual(2, tree.GetInt("render/quality"));
            Assert.AreEqual("hero", tree.GetString("name"));
            Assert.AreEqual(0.5f, tree.GetFloat("audio/volume"));
            Assert.IsFalse(tree.Has("batchmode"));
        }

        [Test]
        public void Load_FlagWithoutValue_IsTrue()
        {
            var source = new CommandLinePropertySource(new[] { "--debug", "--render/quality=1" });
            var tree = source.Load();
            Assert.AreEqual(true, tree.GetBool("debug"));
            Assert.AreEqual(1, tree.GetInt("render/quality"));
        }

        [Test]
        public void Load_ParsesVectorsAndHexColor()
        {
            var source = new CommandLinePropertySource(new[]
            {
                "--offset=1,2",
                "--tint=#00FF00"
            });
            var tree = source.Load();
            Assert.AreEqual(1f, tree.GetVector2("offset").x);
            Assert.AreEqual(2f, tree.GetVector2("offset").y);
            Assert.AreEqual(1f, tree.GetColor("tint").g);
        }

        [Test]
        public void Load_DoesNotTreatExecutablePathAsKey()
        {
            var source = new CommandLinePropertySource(new[] { "/Applications/Unity/Unity.app/Contents/MacOS/Unity" });
            var tree = source.Load();
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Load_EmptyEquals_IsEmptyString()
        {
            var source = new CommandLinePropertySource(new[] { "--title=" });
            var tree = source.Load();
            Assert.AreEqual(string.Empty, tree.GetString("title"));
        }
    }
}
