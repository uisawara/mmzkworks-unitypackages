using System.Collections.Generic;
using System.Linq;
using Mmzkworks.muDashboard.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muDashboard.Tests
{
    public sealed class DashboardDataTests
    {
        [Test]
        public void RoundTrip_KeepsItems()
        {
            var doc = new DashboardDocument();
            doc.items.Add(new DashboardItem
            {
                id = "a", kind = ShortcutItemKind.Asset, guid = "0123", path = "Assets/Foo.prefab",
                Rect = new Rect(8, 16, 160, 24)
            });
            doc.items.Add(new DashboardItem
            {
                id = "b", kind = ShortcutItemKind.Url, url = "https://example.com/", title = "Ex",
                Rect = new Rect(0, 48, 120, 32)
            });
            doc.items.Add(new DashboardItem
            {
                id = "c", kind = ShortcutItemKind.Label, title = "Hello", fontSize = 14,
                Rect = new Rect(200, 0, 80, 40)
            });

            var back = DashboardSerializer.FromJson(DashboardSerializer.ToJson(doc));

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
            var doc = new DashboardDocument();
            doc.items.Add(new DashboardItem
            {
                id = "act", kind = ShortcutItemKind.Action, steps = new List<string> { "b", "a" },
                Rect = new Rect(0, 0, 100, 24)
            });

            var back = DashboardSerializer.FromJson(DashboardSerializer.ToJson(doc));

            Assert.That(back.items[0].kind, Is.EqualTo(ShortcutItemKind.Action));
            Assert.That(back.items[0].steps, Is.EqualTo(new[] { "b", "a" }));
        }

        [Test]
        public void FromJson_OldFileWithoutSteps_GetsEmptyList()
        {
            var doc = DashboardSerializer.FromJson("{\"items\":[{\"id\":\"m\",\"kind\":\"menu\",\"width\":100,\"height\":24}]}");
            Assert.That(doc.items[0].steps, Is.Not.Null.And.Empty);
        }

        [Test]
        public void Normalize_DropsEmptyStepIds()
        {
            var doc = new DashboardDocument();
            doc.items.Add(new DashboardItem { id = "act", kind = ShortcutItemKind.Action, steps = new List<string> { "a", "", null, "b" } });

            DashboardSerializer.Normalize(doc);

            Assert.That(doc.items[0].steps, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void RoundTrip_KeepsCommandAndArgs()
        {
            var doc = new DashboardDocument();
            var item = new DashboardItem { id = "c", kind = ShortcutItemKind.Command, command = "prefab.create", Rect = new Rect(0, 0, 100, 24) };
            item.SetArg("folder", "Assets/Prefabs");
            item.SetArg("connect", "false");
            doc.items.Add(item);

            var back = DashboardSerializer.FromJson(DashboardSerializer.ToJson(doc)).items[0];

            Assert.That(back.kind, Is.EqualTo(ShortcutItemKind.Command));
            Assert.That(back.command, Is.EqualTo("prefab.create"));
            Assert.That(back.GetArg("folder"), Is.EqualTo("Assets/Prefabs"));
            Assert.That(back.GetArg("connect"), Is.EqualTo("false"));
            Assert.That(back.GetArg("missing"), Is.Null);
        }

        [Test]
        public void SetArg_ReplacesExistingKey()
        {
            var item = new DashboardItem();
            item.SetArg("a", "1");
            item.SetArg("a", "2");
            Assert.That(item.args.Count, Is.EqualTo(1));
            Assert.That(item.GetArg("a"), Is.EqualTo("2"));
        }

        [Test]
        public void Clone_DoesNotShareArgs()
        {
            var original = new DashboardItem { kind = ShortcutItemKind.Command, command = "go.rename" };
            original.SetArg("pattern", "A");

            var clone = original.Clone();
            clone.SetArg("pattern", "B");

            Assert.That(original.GetArg("pattern"), Is.EqualTo("A"));
            Assert.That(clone.command, Is.EqualTo("go.rename"));
        }

        [Test]
        public void Clone_DoesNotShareSteps()
        {
            var original = new DashboardItem { id = "act", kind = ShortcutItemKind.Action, steps = new List<string> { "a" } };

            var clone = original.Clone();
            clone.steps.Add("b");

            Assert.That(clone.id, Is.Not.EqualTo(original.id));
            Assert.That(original.steps, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void RemoveStepReferences_RemovesFromEveryAction()
        {
            var doc = new DashboardDocument();
            doc.items.Add(new DashboardItem { id = "x", kind = ShortcutItemKind.Action, steps = new List<string> { "a", "b", "c" } });
            doc.items.Add(new DashboardItem { id = "y", kind = ShortcutItemKind.Action, steps = new List<string> { "b" } });

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
            var doc = DashboardSerializer.FromJson(json);
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

            var doc = DashboardSerializer.FromJson(json);

            Assert.That(doc.items[0].kind, Is.EqualTo(ShortcutItemKind.Label));
            Assert.That(doc.items[0].x, Is.EqualTo(-10f), "negative positions are kept (unbounded canvas)");
            Assert.That(doc.items[0].width, Is.EqualTo(DashboardLayout.MinWidth));
            Assert.That(doc.items[0].height, Is.EqualTo(DashboardLayout.MinHeight));
            var ids = doc.items.Select(i => i.id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(3), "duplicate / missing ids are regenerated");
            Assert.That(ids.All(i => !string.IsNullOrEmpty(i)));
        }

        [TestCase("Assets/A.dashboard.json", true)]
        [TestCase("Assets/A.DASHBOARD.JSON", true)]
        [TestCase("Assets/A.shortcutpanel.json", false)]
        [TestCase("Assets/dashboard.json", false)]
        [TestCase("Assets/A.json", false)]
        [TestCase(null, false)]
        public void IsPanelFile(string path, bool expected)
        {
            Assert.That(DashboardFiles.IsPanelFile(path), Is.EqualTo(expected));
        }

        [Test]
        public void DisplayName_StripsSuffix()
        {
            Assert.That(DashboardFiles.DisplayName("Assets/Tools/Work.dashboard.json"), Is.EqualTo("Work"));
        }

        [TestCase("Assets/New.json", "Assets/New.dashboard.json")]
        [TestCase("Assets/New.dashboard.json", "Assets/New.dashboard.json")]
        [TestCase("Assets/New.dashboard", "Assets/New.dashboard.json")]
        [TestCase("Assets/New", "Assets/New.dashboard.json")]
        public void EnsureSuffix(string input, string expected)
        {
            Assert.That(DashboardFiles.EnsureSuffix(input), Is.EqualTo(expected));
        }

        [Test]
        public void Snap_RoundsToGrid()
        {
            Assert.That(DashboardLayout.Snap(3f), Is.EqualTo(0f));
            Assert.That(DashboardLayout.Snap(5f), Is.EqualTo(8f));
            Assert.That(DashboardLayout.Snap(13f), Is.EqualTo(16f));
        }

        [Test]
        public void Moved_Snaps_AndAllowsNegativePositions()
        {
            var r = DashboardLayout.Moved(new Rect(16, 16, 100, 20), new Vector2(-40, 5));
            Assert.That(r, Is.EqualTo(new Rect(-24, 24, 100, 20)));
        }

        [Test]
        public void Bounds_CoversAllItems()
        {
            Assert.That(DashboardLayout.Bounds(new List<DashboardItem>()), Is.Null);
            var items = new List<DashboardItem>
            {
                new DashboardItem { Rect = new Rect(-16, 20, 100, 30) },
                new DashboardItem { Rect = new Rect(40, -8, 50, 10) }
            };
            Assert.That(DashboardLayout.Bounds(items), Is.EqualTo(Rect.MinMaxRect(-16, -8, 90, 50)));
        }

        [Test]
        public void Resized_SnapsAndKeepsMinimum()
        {
            var r = DashboardLayout.Resized(new Rect(0, 0, 100, 40), new Vector2(13, -100));
            Assert.That(r.width, Is.EqualTo(112f));
            Assert.That(r.height, Is.EqualTo(DashboardLayout.MinHeight));
        }

        [Test]
        public void HitTest_ReturnsTopmost()
        {
            var items = new List<DashboardItem>
            {
                new DashboardItem { id = "back", Rect = new Rect(0, 0, 100, 100) },
                new DashboardItem { id = "front", Rect = new Rect(50, 50, 100, 100) }
            };

            Assert.That(DashboardLayout.HitTest(items, new Vector2(60, 60)).id, Is.EqualTo("front"));
            Assert.That(DashboardLayout.HitTest(items, new Vector2(10, 10)).id, Is.EqualTo("back"));
            Assert.That(DashboardLayout.HitTest(items, new Vector2(300, 300)), Is.Null);
        }

        [Test]
        public void ContentSize_IncludesMargin()
        {
            var items = new List<DashboardItem>
            {
                new DashboardItem { Rect = new Rect(10, 20, 100, 30) },
                new DashboardItem { Rect = new Rect(0, 100, 50, 10) }
            };
            Assert.That(DashboardLayout.ContentSize(items, 64f), Is.EqualTo(new Vector2(174f, 174f)));
        }
    }
}
