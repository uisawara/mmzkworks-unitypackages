using System;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muProperty.Tests
{
    public class PropertyTreeTests
    {
        [Test]
        public void SetAndGet_AllLeafTypes()
        {
            var tree = new PropertyTree();
            tree.Set("flag", true);
            tree.Set("count", 3);
            tree.Set("scale", 1.5f);
            tree.Set("v2", new Vector2(1f, 2f));
            tree.Set("v3", new Vector3(1f, 2f, 3f));
            tree.Set("v4", new Vector4(1f, 2f, 3f, 4f));
            tree.Set("tint", new Color(1f, 0f, 0f, 1f));
            tree.Set("name", "hero");

            Assert.AreEqual(true, tree.GetBool("flag"));
            Assert.AreEqual(3, tree.GetInt("count"));
            Assert.AreEqual(1.5f, tree.GetFloat("scale"));
            Assert.AreEqual(1f, tree.GetVector2("v2").x);
            Assert.AreEqual(2f, tree.GetVector2("v2").y);
            Assert.AreEqual(3f, tree.GetVector3("v3").z);
            Assert.AreEqual(4f, tree.GetVector4("v4").w);
            Assert.AreEqual(1f, tree.GetColor("tint").r);
            Assert.AreEqual("hero", tree.GetString("name"));
        }

        [Test]
        public void SlashPath_CreatesNestedObjects()
        {
            var tree = new PropertyTree();
            tree.Set("render/quality", 2);

            Assert.IsTrue(tree.Has("render"));
            Assert.IsTrue(tree.Has("render/quality"));
            Assert.AreEqual(2, tree.GetInt("render/quality"));
            Assert.IsTrue(tree.TryGetObject("render", out var render));
            Assert.AreEqual(2, render.GetInt("quality"));
        }

        [Test]
        public void LeadingAndTrailingSlashes_AreIgnored()
        {
            var tree = new PropertyTree();
            tree.Set("/render/quality/", 2);
            Assert.AreEqual(2, tree.GetInt("render/quality"));
            Assert.AreEqual(2, tree.GetInt("/render/quality/"));
        }

        [Test]
        public void EmptySegment_Throws()
        {
            var tree = new PropertyTree();
            Assert.Throws<ArgumentException>(() => tree.Set("a//b", 1));
        }

        [Test]
        public void NullPath_Throws()
        {
            var tree = new PropertyTree();
            Assert.Throws<ArgumentNullException>(() => tree.Set(null, 1));
        }

        [Test]
        public void Get_MissingOrWrongType_ReturnsDefault()
        {
            var tree = new PropertyTree();
            tree.Set("n", 1);
            Assert.AreEqual(0, tree.GetInt("missing"));
            Assert.AreEqual(9, tree.GetInt("missing", 9));
            Assert.AreEqual(0, tree.GetInt("n/child"));
            Assert.IsFalse(tree.GetBool("n"));
        }

        [Test]
        public void TryGet_ObjectPath_FailsForLeafGetter()
        {
            var tree = new PropertyTree();
            tree.Set("a/b", 1);
            Assert.IsFalse(tree.TryGet("a", out _));
            Assert.IsTrue(tree.TryGet("a/b", out var value));
            Assert.AreEqual(1, value.AsInt());
        }

        [Test]
        public void Set_OverwritesLeafWithObject()
        {
            var tree = new PropertyTree();
            tree.Set("a", 1);
            tree.Set("a/b", 2);
            Assert.IsFalse(tree.TryGet("a", out _));
            Assert.AreEqual(2, tree.GetInt("a/b"));
        }

        [Test]
        public void SetSubtree_IsCloned()
        {
            var child = new PropertyTree();
            child.Set("x", 1);
            var tree = new PropertyTree();
            tree.Set("child", child);
            child.Set("x", 99);
            Assert.AreEqual(1, tree.GetInt("child/x"));
        }

        [Test]
        public void Clone_IsDeepAndIndependent()
        {
            var tree = new PropertyTree();
            tree.Set("a/b", 1);
            var clone = tree.Clone();
            tree.Set("a/b", 2);
            tree.Set("a/c", 3);
            Assert.AreEqual(1, clone.GetInt("a/b"));
            Assert.IsFalse(clone.Has("a/c"));
        }

        [Test]
        public void DeepEquals_ComparesStructureAndValues()
        {
            var a = new PropertyTree();
            a.Set("a/b", 1);
            a.Set("name", "x");
            var b = new PropertyTree();
            b.Set("name", "x");
            b.Set("a/b", 1);
            Assert.IsTrue(a.DeepEquals(b));
            b.Set("a/b", 2);
            Assert.IsFalse(a.DeepEquals(b));
        }

        [Test]
        public void Keys_AreImmediateChildren()
        {
            var tree = new PropertyTree();
            tree.Set("a/b", 1);
            tree.Set("c", 2);
            CollectionAssert.AreEquivalent(new[] { "a", "c" }, tree.Keys);
        }
    }
}
