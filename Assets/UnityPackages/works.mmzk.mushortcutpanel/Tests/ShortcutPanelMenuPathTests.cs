using Mmzkworks.muShortcutPanel.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muShortcutPanel.Tests
{
    public sealed class ShortcutPanelMenuPathTests
    {
        [TestCase("Window/General/Console", ShortcutMenuTarget.Editor)]
        [TestCase("Tools/muShortcutPanel/ShortcutPanel", ShortcutMenuTarget.Editor)]
        [TestCase("Assets/Create/Folder", ShortcutMenuTarget.Project)]
        [TestCase("GameObject/Create Empty", ShortcutMenuTarget.Hierarchy)]
        [TestCase("AssetsExtra/Foo", ShortcutMenuTarget.Editor)]
        [TestCase("", ShortcutMenuTarget.Editor)]
        public void TargetOf(string path, string expected)
        {
            Assert.That(ShortcutPanelMenuPath.TargetOf(path), Is.EqualTo(expected));
        }

        [TestCase("Tools/Foo %#g", "Tools/Foo")]
        [TestCase("Tools/Foo _F5", "Tools/Foo")]
        [TestCase("Tools/Foo &x", "Tools/Foo")]
        [TestCase("GameObject/Create Empty", "GameObject/Create Empty")]
        [TestCase("Tools/Foo", "Tools/Foo")]
        public void StripHotkey(string input, string expected)
        {
            Assert.That(ShortcutPanelMenuPath.StripHotkey(input), Is.EqualTo(expected));
        }

        [Test]
        public void DefaultTitleAndParent()
        {
            Assert.That(ShortcutPanelMenuPath.DefaultTitle("Window/General/Console"), Is.EqualTo("Console"));
            Assert.That(ShortcutPanelMenuPath.ParentPath("Window/General/Console"), Is.EqualTo("Window/General"));
        }

        [TestCase("Window/General/Console", true, "Window/General/Console")]
        [TestCase("  Assets/Create/Folder  ", true, "Assets/Create/Folder")]
        [TestCase("https://example.com/a", false, null)]
        [TestCase("Window", false, null)]
        [TestCase("Window//Console", false, null)]
        [TestCase("Window/General\nConsole", false, null)]
        [TestCase("", false, null)]
        public void TryParse(string text, bool ok, string expected)
        {
            Assert.That(ShortcutPanelMenuPath.TryParse(text, out var path), Is.EqualTo(ok));
            Assert.That(path, Is.EqualTo(expected));
        }

        [Test]
        public void GroupMoveOffset_SnapsPrimary_AndAllowsNegativePositions()
        {
            var primary = new Rect(16, 16, 100, 24);

            var offset = ShortcutPanelLayout.GroupMoveOffset(primary, new Vector2(10, 3));
            Assert.That(offset, Is.EqualTo(new Vector2(8, 0)));

            // The canvas is unbounded: the group may go left of / above the origin.
            offset = ShortcutPanelLayout.GroupMoveOffset(primary, new Vector2(-40, -40));
            Assert.That(offset, Is.EqualTo(new Vector2(-40, -40)));
        }

        [Test]
        public void MenuItem_RoundTrips()
        {
            var doc = new ShortcutPanelDocument();
            doc.items.Add(new ShortcutPanelItem
            {
                id = "m", kind = ShortcutItemKind.Menu, menu = "GameObject/Create Empty", Rect = new Rect(0, 0, 160, 24)
            });

            var back = ShortcutPanelSerializer.FromJson(ShortcutPanelSerializer.ToJson(doc));

            Assert.That(back.items[0].kind, Is.EqualTo(ShortcutItemKind.Menu));
            Assert.That(back.items[0].menu, Is.EqualTo("GameObject/Create Empty"));
        }

        [Test]
        public void RectFromPoints_NormalizesDirection()
        {
            Assert.That(ShortcutPanelLayout.RectFromPoints(new Vector2(50, 10), new Vector2(10, 40)),
                Is.EqualTo(Rect.MinMaxRect(10, 10, 50, 40)));
        }
    }
}
