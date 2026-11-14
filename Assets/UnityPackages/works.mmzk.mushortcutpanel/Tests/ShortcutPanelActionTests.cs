using System.Collections.Generic;
using System.Linq;
using Mmzkworks.muShortcutPanel.Editor;
using NUnit.Framework;

namespace Mmzkworks.muShortcutPanel.Tests
{
    public sealed class ShortcutPanelActionTests
    {
        private static ShortcutPanelItem Item(string id, string kind, params string[] steps) =>
            new ShortcutPanelItem { id = id, kind = kind, title = id, steps = steps.ToList() };

        private static ShortcutPanelDocument Doc(params ShortcutPanelItem[] items)
        {
            var doc = new ShortcutPanelDocument();
            doc.items.AddRange(items);
            return doc;
        }

        [Test]
        public void Expand_KeepsOrder()
        {
            var doc = Doc(
                Item("act", ShortcutItemKind.Action, "m", "a", "u"),
                Item("a", ShortcutItemKind.Asset),
                Item("u", ShortcutItemKind.Url),
                Item("m", ShortcutItemKind.Menu));

            var steps = ShortcutPanelActionPlan.Expand(doc, "act", out var error);

            Assert.That(error, Is.Null);
            Assert.That(steps.Select(s => s.id), Is.EqualTo(new[] { "m", "a", "u" }));
        }

        [Test]
        public void Expand_ExpandsNestedActionsInPlace_AndSkipsLabels()
        {
            var doc = Doc(
                Item("outer", ShortcutItemKind.Action, "a", "inner", "l", "b"),
                Item("inner", ShortcutItemKind.Action, "c", "d"),
                Item("a", ShortcutItemKind.Menu),
                Item("b", ShortcutItemKind.Menu),
                Item("c", ShortcutItemKind.Menu),
                Item("d", ShortcutItemKind.Menu),
                Item("l", ShortcutItemKind.Label));

            var steps = ShortcutPanelActionPlan.Expand(doc, "outer", out var error);

            Assert.That(error, Is.Null);
            Assert.That(steps.Select(s => s.id), Is.EqualTo(new[] { "a", "c", "d", "b" }));
        }

        [Test]
        public void Expand_SameNestedActionTwice_IsNotACycle()
        {
            var doc = Doc(
                Item("outer", ShortcutItemKind.Action, "inner", "inner2"),
                Item("inner2", ShortcutItemKind.Action, "inner"),
                Item("inner", ShortcutItemKind.Action, "m"),
                Item("m", ShortcutItemKind.Menu));

            var steps = ShortcutPanelActionPlan.Expand(doc, "outer", out var error);

            Assert.That(error, Is.Null);
            Assert.That(steps.Select(s => s.id), Is.EqualTo(new[] { "m", "m" }));
        }

        [Test]
        public void Expand_SelfCycle_IsError()
        {
            var doc = Doc(Item("act", ShortcutItemKind.Action, "act"));

            Assert.That(ShortcutPanelActionPlan.Expand(doc, "act", out var error), Is.Null);
            Assert.That(error, Does.Contain("cycle"));
        }

        [Test]
        public void Expand_MutualCycle_IsError()
        {
            var doc = Doc(
                Item("a", ShortcutItemKind.Action, "m", "b"),
                Item("b", ShortcutItemKind.Action, "a"),
                Item("m", ShortcutItemKind.Menu));

            Assert.That(ShortcutPanelActionPlan.Expand(doc, "a", out var error), Is.Null);
            Assert.That(error, Does.Contain("cycle"));
        }

        [Test]
        public void Expand_MissingStep_IsError()
        {
            var doc = Doc(Item("act", ShortcutItemKind.Action, "gone"));

            Assert.That(ShortcutPanelActionPlan.Expand(doc, "act", out var error), Is.Null);
            Assert.That(error, Does.Contain("Missing"));
        }

        [Test]
        public void Expand_NotAnAction_IsError()
        {
            var doc = Doc(Item("m", ShortcutItemKind.Menu));

            Assert.That(ShortcutPanelActionPlan.Expand(doc, "m", out var error), Is.Null);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void TryAdd_AddsInOrder_AndRejectsDuplicatesSelfLabelsAndNull()
        {
            var action = Item("act", ShortcutItemKind.Action);
            var steps = new List<string>();

            Assert.That(ShortcutPanelActionTrace.TryAdd(steps, action, Item("a", ShortcutItemKind.Asset)), Is.True);
            Assert.That(ShortcutPanelActionTrace.TryAdd(steps, action, Item("b", ShortcutItemKind.Menu)), Is.True);
            Assert.That(ShortcutPanelActionTrace.TryAdd(steps, action, Item("a", ShortcutItemKind.Asset)), Is.False, "duplicate");
            Assert.That(ShortcutPanelActionTrace.TryAdd(steps, action, action), Is.False, "self");
            Assert.That(ShortcutPanelActionTrace.TryAdd(steps, action, Item("l", ShortcutItemKind.Label)), Is.False, "label");
            Assert.That(ShortcutPanelActionTrace.TryAdd(steps, action, Item("i", ShortcutItemKind.Image)), Is.False, "image");
            Assert.That(ShortcutPanelActionTrace.TryAdd(steps, action, null), Is.False, "null");
            Assert.That(ShortcutPanelActionTrace.TryAdd(steps, action, Item("other", ShortcutItemKind.Action)), Is.True, "nested action");

            Assert.That(steps, Is.EqualTo(new[] { "a", "b", "other" }));
        }
    }
}
