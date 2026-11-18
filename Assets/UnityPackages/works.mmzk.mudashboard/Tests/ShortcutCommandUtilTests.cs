using System.Linq;
using Mmzkworks.muDashboard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muDashboard.Tests
{
    public sealed class ShortcutCommandUtilTests
    {
        [TestCase("", "Tree", 1, "Tree")]
        [TestCase(null, "Tree", 1, "Tree")]
        [TestCase("{name}", "Tree", 3, "Tree")]
        [TestCase("Tree_{index}", "Oak", 2, "Tree_2")]
        [TestCase("{name}_{index}_{name}", "A", 10, "A_10_A")]
        [TestCase("Fixed", "Oak", 1, "Fixed")]
        public void ExpandName(string pattern, string name, int index, string expected)
        {
            Assert.That(ShortcutCommandUtil.ExpandName(pattern, name, index), Is.EqualTo(expected));
        }

        [Test]
        public void SanitizeFileName_ReplacesInvalidCharacters()
        {
            Assert.That(ShortcutCommandUtil.SanitizeFileName(" a/b "), Does.Not.Contain("/"));
            Assert.That(ShortcutCommandUtil.SanitizeFileName(" Tree "), Is.EqualTo("Tree"));
            Assert.That(ShortcutCommandUtil.SanitizeFileName(null), Is.EqualTo(""));
        }

        [Test]
        public void Vector3_RoundTrip_UsesInvariantCulture()
        {
            var v = new Vector3(1.5f, -2f, 0.25f);
            var text = ShortcutCommandUtil.FormatVector3(v);
            Assert.That(text, Is.EqualTo("1.5,-2,0.25"));
            Assert.That(ShortcutCommandUtil.TryParseVector3(text, out var back), Is.True);
            Assert.That(back, Is.EqualTo(v));
        }

        [TestCase("")]
        [TestCase("1,2")]
        [TestCase("1,x,3")]
        public void TryParseVector3_RejectsInvalid(string text)
        {
            Assert.That(ShortcutCommandUtil.TryParseVector3(text, out _), Is.False);
        }

        [TestCase("Assets", "Assets")]
        [TestCase("Assets/Prefabs/", "Assets/Prefabs")]
        [TestCase(" Assets\\Prefabs\\Env ", "Assets/Prefabs/Env")]
        [TestCase("Assets//A", "Assets/A")]
        [TestCase("Packages/com.foo/Runtime", "Packages/com.foo/Runtime")]
        [TestCase("Prefabs", null)]
        [TestCase("C:/Project/Assets", null)]
        [TestCase("", null)]
        public void NormalizeAssetFolder(string input, string expected)
        {
            Assert.That(ShortcutCommandUtil.NormalizeAssetFolder(input), Is.EqualTo(expected));
        }

        [TestCase("C:/Work/Proj/Assets/Prefabs", "C:/Work/Proj", "Assets/Prefabs")]
        [TestCase("C:\\Work\\Proj\\Assets", "C:\\Work\\Proj\\", "Assets")]
        [TestCase("D:/Other/Assets", "C:/Work/Proj", null)]
        [TestCase("", "C:/Work/Proj", null)]
        public void ToProjectRelative(string absolute, string root, string expected)
        {
            Assert.That(ShortcutCommandUtil.ToProjectRelative(absolute, root), Is.EqualTo(expected));
        }

        [Test]
        public void ActiveProjectFolder_IsAnExistingFolderUnderAssets()
        {
            var folder = ShortcutCommandUtil.ActiveProjectFolder();
            Assert.That(folder, Does.StartWith("Assets"));
            Assert.That(UnityEditor.AssetDatabase.IsValidFolder(folder), Is.True);
        }

        [Test]
        public void SplitHierarchyPath_TrimsAndDropsEmptySegments()
        {
            Assert.That(ShortcutCommandUtil.SplitHierarchyPath("/Env//Trees/ Oak "), Is.EqualTo(new[] { "Env", "Trees", "Oak" }));
            Assert.That(ShortcutCommandUtil.SplitHierarchyPath("  "), Is.Empty);
        }

        [Test]
        public void FindByPath_AndTopLevelOnly()
        {
            var env = new GameObject("Env");
            try
            {
                var trees = new GameObject("Trees");
                trees.transform.SetParent(env.transform);
                var oak1 = new GameObject("Oak");
                oak1.transform.SetParent(trees.transform);
                var oak2 = new GameObject("Oak");
                oak2.transform.SetParent(trees.transform);

                var found = ShortcutCommandUtil.FindByPath(new[] { env }, "Env/Trees/Oak");
                Assert.That(found, Is.EquivalentTo(new[] { oak1, oak2 }));
                Assert.That(ShortcutCommandUtil.FindByPath(new[] { env }, "Env/Oak"), Is.Empty);
                Assert.That(ShortcutCommandUtil.HierarchyPath(oak1.transform), Is.EqualTo("Env/Trees/Oak"));

                var top = ShortcutCommandUtil.TopLevelOnly(new[] { oak1, trees, env, oak1 });
                Assert.That(top, Is.EqualTo(new[] { env }));
            }
            finally
            {
                Object.DestroyImmediate(env);
            }
        }

        [Test]
        public void ResolveComponentType()
        {
            Assert.That(ShortcutCommandUtil.ResolveComponentType("Rigidbody", out var e1), Is.EqualTo(typeof(Rigidbody)));
            Assert.That(e1, Is.Null);
            Assert.That(ShortcutCommandUtil.ResolveComponentType("UnityEngine.BoxCollider", out _), Is.EqualTo(typeof(BoxCollider)));
            Assert.That(ShortcutCommandUtil.ResolveComponentType("NoSuchComponentXyz", out var e2), Is.Null);
            Assert.That(e2, Does.Contain("not found"));
            Assert.That(ShortcutCommandUtil.ResolveComponentType(" ", out var e3), Is.Null);
            Assert.That(e3, Is.Not.Null);
        }
    }

    public sealed class ShortcutCommandRegistryTests
    {
        [Test]
        public void Ids_AreUnique_AndEveryCommandIsComplete()
        {
            var all = ShortcutCommandRegistry.All;
            Assert.That(all, Is.Not.Empty);
            Assert.That(all.Select(c => c.Id).Distinct().Count(), Is.EqualTo(all.Count));
            foreach (var c in all)
            {
                Assert.That(c.Name, Is.Not.Empty, c.Id);
                Assert.That(c.Description, Is.Not.Empty, c.Id);
                Assert.That(ShortcutCommandCategory.All, Does.Contain(c.Category), c.Id);
                Assert.That(c.Run, Is.Not.Null, c.Id);
                Assert.That(c.Params.Select(p => p.Key).Distinct().Count(), Is.EqualTo(c.Params.Length), c.Id);
                foreach (var p in c.Params.Where(p => p.Kind == ShortcutParamKind.Choice))
                    Assert.That(p.Choices, Does.Contain(p.Default), $"{c.Id}.{p.Key}");
            }
        }

        [Test]
        public void Find_ReturnsNullForUnknown()
        {
            Assert.That(ShortcutCommandRegistry.Find("prefab.create"), Is.Not.Null);
            Assert.That(ShortcutCommandRegistry.Find("nope"), Is.Null);
            Assert.That(ShortcutCommandRegistry.Find(null), Is.Null);
        }

        [Test]
        public void Context_UsesDefaultsUntilSet()
        {
            var command = ShortcutCommandRegistry.Find("prefab.create");
            var item = new DashboardItem { kind = ShortcutItemKind.Command, command = command.Id };
            var ctx = new ShortcutCommandContext(command, item);

            Assert.That(ctx.GetString("folder"), Is.Empty, "empty = the Project window's folder");
            Assert.That(ctx.GetBool("connect"), Is.True);
            Assert.That(command.Summary(item), Does.Contain("(Project folder)"));

            item.SetArg("folder", "Assets/Other");
            item.SetArg("connect", "false");
            Assert.That(ctx.GetString("folder"), Is.EqualTo("Assets/Other"));
            Assert.That(ctx.GetBool("connect"), Is.False);
            Assert.That(item.args.Count, Is.EqualTo(2));
        }

        [Test]
        public void OpenDashboard_AcceptsOnlyPanelFiles()
        {
            var command = ShortcutCommandRegistry.Find("dashboard.open");
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Category, Is.EqualTo(ShortcutCommandCategory.Dashboard));

            var param = command.FindParam("dashboard");
            Assert.That(param.AcceptsAssetPath("Assets/Work.dashboard.json"), Is.True);
            Assert.That(param.AcceptsAssetPath("Assets/data.json"), Is.False);
            Assert.That(param.AcceptsAssetPath("Assets/dashboard.json"), Is.False);
        }

        [Test]
        public void OpenDashboard_FailsWithoutDashboard()
        {
            var command = ShortcutCommandRegistry.Find("dashboard.open");
            var item = new DashboardItem { kind = ShortcutItemKind.Command, command = command.Id };
            var previous = Selection.objects;
            try
            {
                Selection.objects = new Object[0];
                var ctx = new ShortcutCommandContext(command, item);
                Assert.That(command.Run(ctx), Is.False);
                Assert.That(ctx.Error, Does.Contain("dashboard"));
            }
            finally
            {
                Selection.objects = previous;
            }
        }
    }
}
