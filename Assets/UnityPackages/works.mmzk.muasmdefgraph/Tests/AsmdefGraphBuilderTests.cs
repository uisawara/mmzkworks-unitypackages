using System.Collections.Generic;
using NUnit.Framework;

namespace Mmzkworks.muAsmdefgraph.Tests
{
    public class AsmdefGraphBuilderTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            AsmdefDatabase.Refresh();
        }

        [Test]
        public void BuildLevels_EmptyRoots_ReturnsEmptyLevels()
        {
            var roots = new HashSet<string>();
            var levels = AsmdefGraphBuilder.BuildLevels(roots, 10, false, false);
            Assert.IsNotNull(levels);
            Assert.AreEqual(0, levels.Count);
        }

        [Test]
        public void BuildLevels_UnknownRoot_ReturnsSingleLevelWithThatName()
        {
            var roots = new HashSet<string> { "NonExistent.Asmdef.Name" };
            var levels = AsmdefGraphBuilder.BuildLevels(roots, 10, false, false);
            Assert.IsNotNull(levels);
            Assert.GreaterOrEqual(levels.Count, 1);
            Assert.AreEqual(1, levels[0].Count);
            Assert.AreEqual("NonExistent.Asmdef.Name", levels[0][0]);
        }

        [Test]
        public void BuildLevels_AfterRefresh_WithKnownAsmdef_FirstLevelContainsRoot()
        {
            string anyDisplayName = null;
            if (!AsmdefDatabase.TryGetPathByDisplayName("works.mmzk.muasmdefgraph.Editor", out _))
            {
                if (!AsmdefDatabase.TryGetPathByAsmdefName("works.mmzk.muasmdefgraph.Editor", out var path))
                {
                    Assert.Ignore("No asmdef found in project to use as root (e.g. works.mmzk.muasmdefgraph.Editor)");
                    return;
                }
                if (!AsmdefDatabase.TryGetDisplayNameForPath(path, out anyDisplayName))
                    anyDisplayName = "works.mmzk.muasmdefgraph.Editor";
            }
            else
                anyDisplayName = "works.mmzk.muasmdefgraph.Editor";

            if (string.IsNullOrEmpty(anyDisplayName))
            {
                Assert.Ignore("Could not resolve display name for test root");
                return;
            }

            var roots = new HashSet<string> { anyDisplayName };
            var levels = AsmdefGraphBuilder.BuildLevels(roots, 5, false, false);
            Assert.IsNotNull(levels);
            Assert.GreaterOrEqual(levels.Count, 1);
            Assert.IsTrue(levels[0].Contains(anyDisplayName));
        }

        [Test]
        public void ComputeDependencyStats_NullRoots_ReturnsZero()
        {
            var (direct, total) = AsmdefGraphBuilder.ComputeDependencyStats(null);
            Assert.AreEqual(0, direct);
            Assert.AreEqual(0, total);
        }

        [Test]
        public void ComputeDependencyStats_EmptyRoots_ReturnsZero()
        {
            var roots = new HashSet<string>();
            var (direct, total) = AsmdefGraphBuilder.ComputeDependencyStats(roots);
            Assert.AreEqual(0, direct);
            Assert.AreEqual(0, total);
        }

        [Test]
        public void ComputeDependencyStats_AfterRefresh_WithKnownRoot_ReturnsNonNegative()
        {
            if (!AsmdefDatabase.TryGetPathByDisplayName("works.mmzk.muasmdefgraph.Editor", out _) &&
                !AsmdefDatabase.TryGetPathByAsmdefName("works.mmzk.muasmdefgraph.Editor", out _))
            {
                Assert.Ignore("No asmdef found in project (e.g. works.mmzk.muasmdefgraph.Editor)");
                return;
            }
            string displayName = "works.mmzk.muasmdefgraph.Editor";
            if (!AsmdefDatabase.TryGetAsmdefInfoByName(displayName, out _))
            {
                Assert.Ignore("Asmdef not in database");
                return;
            }
            var roots = new HashSet<string> { displayName };
            var (directCount, totalCount) = AsmdefGraphBuilder.ComputeDependencyStats(roots);
            Assert.GreaterOrEqual(directCount, 0);
            Assert.GreaterOrEqual(totalCount, 0);
        }
    }
}
