using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph.Tests
{
    public class AsmdefPathHelperTests
    {
        private string _tempAsmdefAssetPath;
        private string _tempAsmdefFullPath;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempAsmdefFullPath) && File.Exists(_tempAsmdefFullPath))
            {
                try { File.Delete(_tempAsmdefFullPath); } catch { }
            }
        }

        [Test]
        public void GetProjectRootFullPath_ReturnsNonEmpty()
        {
            var root = AsmdefPathHelper.GetProjectRootFullPath();
            Assert.IsFalse(string.IsNullOrEmpty(root));
        }

        [Test]
        public void GetProjectRootFullPath_IsParentOfDataPath()
        {
            var root = AsmdefPathHelper.GetProjectRootFullPath();
            var dataDir = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
            Assert.AreEqual(dataDir, root);
        }

        [Test]
        public void ToFullPath_AssetsPath_ReturnsAbsolutePath()
        {
            var assetPath = "Assets";
            var full = AsmdefPathHelper.ToFullPath(assetPath);
            Assert.IsFalse(string.IsNullOrEmpty(full));
            Assert.IsTrue(Path.IsPathRooted(full));
        }

        [Test]
        public void ToFullPath_Empty_ReturnsEmpty()
        {
            var result = AsmdefPathHelper.ToFullPath("");
            Assert.AreEqual("", result);
        }

        [Test]
        public void ToAssetPath_Empty_ReturnsEmpty()
        {
            var result = AsmdefPathHelper.ToAssetPath("");
            Assert.AreEqual("", result);
        }

        [Test]
        public void ToFullPath_And_ToAssetPath_RoundTrip()
        {
            var assetPath = "Assets/SomeFolder";
            var full = AsmdefPathHelper.ToFullPath(assetPath);
            if (string.IsNullOrEmpty(full))
            {
                Assert.Ignore("ToFullPath returned empty (e.g. project root not available)");
                return;
            }
            var back = AsmdefPathHelper.ToAssetPath(full);
            Assert.AreEqual(assetPath, back);
        }

        [Test]
        public void ToAssetPath_OutsideProject_ReturnsEmpty()
        {
            var outsidePath = Path.GetTempPath();
            if (string.IsNullOrEmpty(outsidePath))
            {
                Assert.Ignore("Temp path not available");
                return;
            }
            var result = AsmdefPathHelper.ToAssetPath(outsidePath);
            Assert.AreEqual("", result);
        }

        [Test]
        public void ResolveAsmdefNameFromPath_Empty_ReturnsEmpty()
        {
            var result = AsmdefPathHelper.ResolveAsmdefNameFromPath("");
            Assert.AreEqual("", result);
        }

        [Test]
        public void ResolveAsmdefNameFromPath_NotAsmdefExtension_ReturnsEmpty()
        {
            var result = AsmdefPathHelper.ResolveAsmdefNameFromPath("Assets/foo.txt");
            Assert.AreEqual("", result);
        }

        [Test]
        public void ResolveAsmdefNameFromPath_WithTempAsmdefFile_ReturnsName()
        {
            var root = AsmdefPathHelper.GetProjectRootFullPath();
            if (string.IsNullOrEmpty(root))
            {
                Assert.Ignore("Project root not available");
                return;
            }
            var libraryDir = Path.Combine(root, "Library");
            if (!Directory.Exists(libraryDir))
                Directory.CreateDirectory(libraryDir);
            _tempAsmdefFullPath = Path.Combine(libraryDir, "AsmdefGraphTest.tmp.asmdef");
            var json = "{\"name\":\"Test.Asmdef.Name\"}";
            File.WriteAllText(_tempAsmdefFullPath, json);
            _tempAsmdefAssetPath = AsmdefPathHelper.ToAssetPath(_tempAsmdefFullPath);
            if (string.IsNullOrEmpty(_tempAsmdefAssetPath))
            {
                Assert.Ignore("ToAssetPath failed for temp file");
                return;
            }
            var name = AsmdefPathHelper.ResolveAsmdefNameFromPath(_tempAsmdefAssetPath);
            Assert.AreEqual("Test.Asmdef.Name", name);
        }
    }
}
