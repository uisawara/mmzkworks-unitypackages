using System;
using NUnit.Framework;

namespace Mmzkworks.muSettings.Tests
{
    public class EnvironmentVariablePropertySourceTests
    {
        const string Prefix = "MUSETTINGS_TEST_PX_";
        const string SlashKey = "MUSETTINGS_TEST_SL/quality";

        [SetUp]
        public void SetUp()
        {
            TearDown();
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(Prefix + "render/quality", null);
            Environment.SetEnvironmentVariable(Prefix + "plain", null);
            Environment.SetEnvironmentVariable(SlashKey, null);
        }

        [Test]
        public void Load_Prefix_StripsAndImportsAllMatches()
        {
            Environment.SetEnvironmentVariable(Prefix + "render/quality", "2");
            Environment.SetEnvironmentVariable(Prefix + "plain", "true");
            var tree = new EnvironmentVariablePropertySource(Prefix).Load();
            Assert.AreEqual(2, tree.GetInt("render/quality"));
            Assert.AreEqual(true, tree.GetBool("plain"));
        }

        [Test]
        public void Load_WithoutPrefix_OnlyNamesWithSlash()
        {
            Environment.SetEnvironmentVariable(SlashKey, "9");
            Environment.SetEnvironmentVariable(Prefix + "plain", "true");
            var tree = new EnvironmentVariablePropertySource().Load();
            Assert.AreEqual(9, tree.GetInt(SlashKey));
            Assert.IsFalse(tree.Has("PATH"));
            Assert.IsFalse(tree.Has(Prefix + "plain"));
        }
    }
}
