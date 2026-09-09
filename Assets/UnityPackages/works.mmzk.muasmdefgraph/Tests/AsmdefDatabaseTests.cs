using NUnit.Framework;

namespace Mmzkworks.muAsmdefgraph.Tests
{
    public class AsmdefDatabaseTests
    {
        [SetUp]
        public void SetUp()
        {
            AsmdefDatabase.Refresh();
        }

        [Test]
        public void Refresh_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AsmdefDatabase.Refresh());
        }

        [Test]
        public void TryGetAsmdefInfoByName_EmptyName_ReturnsFalse()
        {
            var result = AsmdefDatabase.TryGetAsmdefInfoByName("", out var info);
            Assert.IsFalse(result);
            Assert.IsNull(info);
        }

        [Test]
        public void TryGetAsmdefInfoByName_NullName_ReturnsFalse()
        {
            var result = AsmdefDatabase.TryGetAsmdefInfoByName(null, out var info);
            Assert.IsFalse(result);
            Assert.IsNull(info);
        }

        [Test]
        public void TryGetAsmdefInfoByName_UnknownName_ReturnsFalse()
        {
            var result = AsmdefDatabase.TryGetAsmdefInfoByName("NonExistent.Asmdef.Name.12345", out var info);
            Assert.IsFalse(result);
            Assert.IsNull(info);
        }

        [Test]
        public void TryGetAsmdefInfoByName_AfterRefresh_WithKnownAsmdef_ReturnsTrueAndNonNullInfo()
        {
            if (!AsmdefDatabase.TryGetPathByAsmdefName("works.mmzk.muasmdefgraph.Editor", out var path))
            {
                Assert.Ignore("Project has no asmdef works.mmzk.muasmdefgraph.Editor");
                return;
            }
            if (!AsmdefDatabase.TryGetDisplayNameForPath(path, out var displayName))
                displayName = "works.mmzk.muasmdefgraph.Editor";

            var result = AsmdefDatabase.TryGetAsmdefInfoByName(displayName, out var info);
            Assert.IsTrue(result);
            Assert.IsNotNull(info);
            Assert.IsNotNull(info.Name);
            Assert.IsNotNull(info.References);
            Assert.IsNotNull(info.PrecompiledReferences);
        }

        [Test]
        public void TryGetPathByDisplayName_UnknownDisplayName_ReturnsFalse()
        {
            var result = AsmdefDatabase.TryGetPathByDisplayName("NonExistent.Display.Name", out var path);
            Assert.IsFalse(result);
            Assert.IsNull(path);
        }

        [Test]
        public void TryGetPathByDisplayName_And_TryGetDisplayNameForPath_RoundTrip()
        {
            if (!AsmdefDatabase.TryGetPathByAsmdefName("works.mmzk.muasmdefgraph.Editor", out var asmdefPath))
            {
                Assert.Ignore("Project has no asmdef works.mmzk.muasmdefgraph.Editor");
                return;
            }
            if (!AsmdefDatabase.TryGetDisplayNameForPath(asmdefPath, out var displayName))
            {
                Assert.Ignore("Display name not found for path");
                return;
            }
            var gotPath = AsmdefDatabase.TryGetPathByDisplayName(displayName, out var pathBack);
            Assert.IsTrue(gotPath);
            Assert.IsFalse(string.IsNullOrEmpty(pathBack));
            Assert.IsTrue(pathBack.EndsWith(".asmdef"));
            if (!AsmdefDatabase.TryGetDisplayNameForPath(pathBack, out var displayNameBack))
            {
                Assert.Fail("Display name not found for path returned by TryGetPathByDisplayName");
                return;
            }
            Assert.AreEqual(displayName, displayNameBack, "Round-trip by display name must be consistent");
        }

        [Test]
        public void TryGetPathByAsmdefName_UnknownName_ReturnsFalse()
        {
            var result = AsmdefDatabase.TryGetPathByAsmdefName("NonExistent.Asmdef", out var path);
            Assert.IsFalse(result);
            Assert.IsNull(path);
        }

        [Test]
        public void TryGetPathByAsmdefName_AfterRefresh_WithExistingAsmdef_ReturnsTrue()
        {
            var result = AsmdefDatabase.TryGetPathByAsmdefName("works.mmzk.muasmdefgraph.Editor", out var path);
            if (!result)
            {
                Assert.Ignore("Project has no asmdef works.mmzk.muasmdefgraph.Editor");
                return;
            }
            Assert.IsFalse(string.IsNullOrEmpty(path));
            Assert.IsTrue(path.EndsWith(".asmdef"));
        }

        [Test]
        public void IsUnresolvedNode_Empty_ReturnsFalse()
        {
            var result = AsmdefDatabase.IsUnresolvedNode("");
            Assert.IsFalse(result);
        }

        [Test]
        public void IsUnresolvedNode_Null_ReturnsFalse()
        {
            var result = AsmdefDatabase.IsUnresolvedNode(null);
            Assert.IsFalse(result);
        }

        [Test]
        public void IsUnresolvedNode_KnownResolvedDisplayName_ReturnsFalse()
        {
            if (!AsmdefDatabase.TryGetPathByAsmdefName("works.mmzk.muasmdefgraph.Editor", out var path))
            {
                Assert.Ignore("Project has no asmdef works.mmzk.muasmdefgraph.Editor");
                return;
            }
            if (!AsmdefDatabase.TryGetDisplayNameForPath(path, out var displayName))
                displayName = "works.mmzk.muasmdefgraph.Editor";
            var result = AsmdefDatabase.IsUnresolvedNode(displayName);
            Assert.IsFalse(result);
        }
    }
}
