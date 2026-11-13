using System.Collections.Generic;
using System.Linq;
using Mmzkworks.muShortcutPanel.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muShortcutPanel.Tests
{
    public sealed class ShortcutPanelDataTests
    {
        [Test]
        public void RoundTrip_KeepsItems()
        {
            var doc = new ShortcutPanelDocument();
            doc.items.Add(new ShortcutPanelItem
            {
                id = "a", kind = ShortcutItemKind.Asset, guid = "0123", path = "Assets/Foo.prefab",
                Rect = new Rect(8, 16, 160, 24)
            });
            doc.items.Add(new ShortcutPanelItem
            {
                id = "b", kind = ShortcutItemKind.Url, url = "https://example.com/", title = "Ex",
                Rect = new Rect(0, 48, 120, 32)
            });
            doc.items.Add(new ShortcutPanelItem
            {
                id = "c", kind = ShortcutItemKind.Label, title = "Hello", fontSize = 14,
                Rect = new Rect(200, 0, 80, 40)
            });

            var back = ShortcutPanelSerializer.FromJson(ShortcutPanelSerializer.ToJson(doc));

            Assert.That(back.items.Select(i => i.id), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(back.items[0].kind, Is.EqualTo(ShortcutItemKind.Asset));
            Assert.That(back.items[0].path, Is.EqualTo("Assets/Foo.prefab"));
            Assert.That(back.items[0].Rect, Is.EqualTo(new Rect(8, 16, 160, 24)));
            Assert.That(back.items[1].url, Is.EqualTo("https://example.com/"));
            Assert.That(back.items[2].title, Is.EqualTo("Hello"));
            Assert.That(back.items[2].fontSize, Is.EqualTo(14));
        }

        [Test]
        public void RoundTrip_KeepsActionSteps()
        {
            var doc = new ShortcutPanelDocument();
            doc.items.Add(new ShortcutPanelItem
            {
                id = "act", kind = ShortcutItemKind.Action, steps = new List<string> { "b", "a" },
                Rect = new Rect(0, 0, 100, 24)
            });

            var back = ShortcutPanelSerializer.FromJson(ShortcutPanelSerializer.ToJson(doc));

            Assert.That(back.items[0].kind, Is.EqualTo(ShortcutItemKind.Action));
            Assert.That(back.items[0].steps, Is.EqualTo(new[] { "b", "a" }));
        }

        [Test]
        public void FromJson_OldFileWithoutSteps_GetsEmptyList()
        {
            var doc = ShortcutPanelSerializer.FromJson("{\"items\":[{\"id\":\"m\",\"kind\":\"menu\",\"width\":100,\"height\":24}]}");
            Assert.That(doc.items[0].steps, Is.Not.Null.And.Empty);
        }

        [Test]
        public void Normalize_DropsEmptyStepIds()
        {
            var doc = new ShortcutPanelDocument();
            doc.items.Add(new ShortcutPanelItem { id = "act", kind = ShortcutItemKind.Action, steps = new List<string> { "a", "", null, "b" } });

            ShortcutPanelSerializer.Normalize(doc);

            Assert.That(doc.items[0].steps, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void Clone_DoesNotShareSteps()
        {
            var original = new ShortcutPanelItem { id = "act", kind = ShortcutItemKind.Action, steps = new List<string> { "a" } };

            var clone = original.Clone();
            clone.steps.Add("b");

            Assert.That(clone.id, Is.Not.EqualTo(original.id));
            Assert.That(original.steps, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void RemoveStepReferences_RemovesFromEveryAction()
        {
            var doc = new ShortcutPanelDocument();
            doc.items.Add(new ShortcutPanelItem { id = "x", kind = ShortcutItemKind.Action, steps = new List<string> { "a", "b", "c" } });
            doc.items.Add(new ShortcutPanelItem { id = "y", kind = ShortcutItemKind.Action, steps = new List<string> { "b" } });

            doc.RemoveStepReferences(new[] { "b", "c" });

            Assert.That(doc.items[0].steps, Is.EqualTo(new[] { "a" }));
            Assert.That(doc.items[1].steps, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{\"items\":")]
        public void FromJson_EmptyOrBroken_ReturnsEmptyDocument(string json)
        {
            var doc = ShortcutPanelSerializer.FromJson(json);
            Assert.That(doc, Is.Not.Null);
            Assert.That(doc.items, Is.Empty);
        }

        [Test]
        public void FromJson_Normalizes()
        {
            const string json = "{\"items\":[" +
                                "{\"id\":\"x\",\"kind\":\"weird\",\"x\":-10,\"y\":5,\"width\":1,\"height\":1}," +
                                "{\"id\":\"x\",\"kind\":\"url\",\"width\":100,\"height\":30}," +
                                "{\"kind\":\"asset\",\"width\":100,\"height\":30}]}";

            var doc = ShortcutPanelSerializer.FromJson(json);

            Assert.That(doc.items[0].kind, Is.EqualTo(ShortcutItemKind.Label));
            Assert.That(doc.items[0].x, Is.EqualTo(0f));
            Assert.That(doc.items[0].width, Is.EqualTo(ShortcutPanelLayout.MinWidth));
            Assert.That(doc.items[0].height, Is.EqualTo(ShortcutPanelLayout.MinHeight));
            var ids = doc.items.Select(i => i.id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(3), "duplicate / missing ids are regenerated");
            Assert.That(ids.All(i => !string.IsNullOrEmpty(i)));
        }

        [TestCase("Assets/A.shortcutpanel.json", true)]
        [TestCase("Assets/A.SHORTCUTPANEL.JSON", true)]
        [TestCase("Assets/A.json", false)]
        [TestCase("Assets/shortcutpanel.json", false)]
        [TestCase(null, false)]
        public void IsPanelFile(string path, bool expected)
        {
            Assert.That(ShortcutPanelFiles.IsPanelFile(path), Is.EqualTo(expected));
        }

        [Test]
        public void DisplayName_StripsSuffix()
        {
            Assert.That(ShortcutPanelFiles.DisplayName("Assets/Tools/Work.shortcutpanel.json"), Is.EqualTo("Work"));
        }

        [TestCase("Assets/New.json", "Assets/New.shortcutpanel.json")]
        [TestCase("Assets/New.shortcutpanel.json", "Assets/New.shortcutpanel.json")]
        [TestCase("Assets/New.shortcutpanel", "Assets/New.shortcutpanel.json")]
        [TestCase("Assets/New", "Assets/New.shortcutpanel.json")]
        public void EnsureSuffix(string input, string expected)
        {
            Assert.That(ShortcutPanelFiles.EnsureSuffix(input), Is.EqualTo(expected));
        }

        [Test]
        public void Snap_RoundsToGrid()
        {
            Assert.That(ShortcutPanelLayout.Snap(3f), Is.EqualTo(0f));
            Assert.That(ShortcutPanelLayout.Snap(5f), Is.EqualTo(8f));
            Assert.That(ShortcutPanelLayout.Snap(13f), Is.EqualTo(16f));
        }

        [Test]
        public void Moved_SnapsAndClampsToOrigin()
        {
            var r = ShortcutPanelLayout.Moved(new Rect(16, 16, 100, 20), new Vector2(-40, 5));
            Assert.That(r, Is.EqualTo(new Rect(0, 24, 100, 20)));
        }

        [Test]
        public void Resized_SnapsAndKeepsMinimum()
        {
            var r = ShortcutPanelLayout.Resized(new Rect(0, 0, 100, 40), new Vector2(13, -100));
            Assert.That(r.width, Is.EqualTo(112f));
            Assert.That(r.height, Is.EqualTo(ShortcutPanelLayout.MinHeight));
        }

        [Test]
        public void HitTest_ReturnsTopmost()
        {
            var items = new List<ShortcutPanelItem>
            {
                new ShortcutPanelItem { id = "back", Rect = new Rect(0, 0, 100, 100) },
                new ShortcutPanelItem { id = "front", Rect = new Rect(50, 50, 100, 100) }
            };

            Assert.That(ShortcutPanelLayout.HitTest(items, new Vector2(60, 60)).id, Is.EqualTo("front"));
            Assert.That(ShortcutPanelLayout.HitTest(items, new Vector2(10, 10)).id, Is.EqualTo("back"));
            Assert.That(ShortcutPanelLayout.HitTest(items, new Vector2(300, 300)), Is.Null);
        }

        [Test]
        public void ContentSize_IncludesMargin()
        {
            var items = new List<ShortcutPanelItem>
            {
                new ShortcutPanelItem { Rect = new Rect(10, 20, 100, 30) },
                new ShortcutPanelItem { Rect = new Rect(0, 100, 50, 10) }
            };
            Assert.That(ShortcutPanelLayout.ContentSize(items, 64f), Is.EqualTo(new Vector2(174f, 174f)));
        }
    }
}
