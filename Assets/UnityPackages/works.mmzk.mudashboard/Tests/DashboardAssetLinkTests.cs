using Mmzkworks.muDashboard.Editor;
using NUnit.Framework;

namespace Mmzkworks.muDashboard.Tests
{
    /// <summary>Asset shortcuts resolve by GUID only; the stored path is never used as a fallback.</summary>
    public sealed class DashboardAssetLinkTests
    {
        // The GUID Unity always gives the Assets folder.
        private const string AssetsFolderGuid = "00000000000000001000000000000000";
        private const string UnknownGuid = "0123456789abcdef0123456789abcdef";

        private static DashboardItem Item(string guid, string path) =>
            new DashboardItem { id = "a", kind = ShortcutItemKind.Asset, guid = guid, path = path };

        [Test]
        public void ResolvesByGuid_EvenWhenTheStoredPathIsStale()
        {
            Assert.That(DashboardWindow.ResolveAssetPath(Item(AssetsFolderGuid, "Assets/Moved Away")), Is.EqualTo("Assets"));
        }

        [Test]
        public void UnknownGuid_IsMissing_EvenWhenTheStoredPathExists()
        {
            Assert.That(DashboardWindow.ResolveAssetPath(Item(UnknownGuid, "Assets")), Is.Null);
        }

        [Test]
        public void EmptyGuid_IsMissing_EvenWhenTheStoredPathExists()
        {
            Assert.That(DashboardWindow.ResolveAssetPath(Item("", "Assets")), Is.Null);
        }
    }
}
