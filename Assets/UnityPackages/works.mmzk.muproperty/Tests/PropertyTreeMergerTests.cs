using NUnit.Framework;

namespace Mmzkworks.muProperty.Tests
{
    public class PropertyTreeMergerTests
    {
        [Test]
        public void Merge_RightOverwritesAndAdds()
        {
            var left = new PropertyTree();
            left.Set("a", 1);
            left.Set("b", 2);
            var right = new PropertyTree();
            right.Set("b", 9);
            right.Set("c", 3);

            var merged = PropertyTreeMerger.Merge(left, right);
            Assert.AreEqual(1, merged.GetInt("a"));
            Assert.AreEqual(9, merged.GetInt("b"));
            Assert.AreEqual(3, merged.GetInt("c"));
        }

        [Test]
        public void Merge_DoesNotMutateInputs()
        {
            var left = new PropertyTree();
            left.Set("b", 2);
            var right = new PropertyTree();
            right.Set("b", 9);

            PropertyTreeMerger.Merge(left, right);
            Assert.AreEqual(2, left.GetInt("b"));
            Assert.AreEqual(9, right.GetInt("b"));
        }

        [Test]
        public void Merge_DeepMergesObjects()
        {
            var left = new PropertyTree();
            left.Set("render/quality", 1);
            left.Set("render/shadows", true);
            var right = new PropertyTree();
            right.Set("render/quality", 2);
            right.Set("audio/volume", 0.5f);

            var merged = PropertyTreeMerger.Merge(left, right);
            Assert.AreEqual(2, merged.GetInt("render/quality"));
            Assert.AreEqual(true, merged.GetBool("render/shadows"));
            Assert.AreEqual(0.5f, merged.GetFloat("audio/volume"));
        }

        [Test]
        public void Merge_LeafReplacesObject()
        {
            var left = new PropertyTree();
            left.Set("a/b", 1);
            var right = new PropertyTree();
            right.Set("a", 9);

            var merged = PropertyTreeMerger.Merge(left, right);
            Assert.AreEqual(9, merged.GetInt("a"));
            Assert.IsFalse(merged.Has("a/b"));
        }

        [Test]
        public void Merge_ObjectReplacesLeaf()
        {
            var left = new PropertyTree();
            left.Set("a", 9);
            var right = new PropertyTree();
            right.Set("a/b", 1);

            var merged = PropertyTreeMerger.Merge(left, right);
            Assert.AreEqual(1, merged.GetInt("a/b"));
            Assert.IsFalse(merged.TryGet("a", out _));
        }

        [Test]
        public void Merge_Params_IsLeftAssociative()
        {
            var a = new PropertyTree();
            a.Set("k", 1);
            var b = new PropertyTree();
            b.Set("k", 2);
            var c = new PropertyTree();
            c.Set("k", 3);

            var merged = PropertyTreeMerger.Merge(a, b, c);
            Assert.AreEqual(3, merged.GetInt("k"));
        }

        [Test]
        public void Extension_MatchesStaticMerger()
        {
            var left = new PropertyTree();
            left.Set("a", 1);
            left.Set("b", 2);
            var right = new PropertyTree();
            right.Set("b", 9);
            right.Set("c", 3);

            var viaStatic = PropertyTreeMerger.Merge(left, right);
            var viaExtension = left.Merge(right);
            Assert.IsTrue(viaStatic.DeepEquals(viaExtension));
        }

        [Test]
        public void Extension_Params_MatchesStaticMerger()
        {
            var a = new PropertyTree();
            a.Set("a", 1);
            var b = new PropertyTree();
            b.Set("b", 2);
            var c = new PropertyTree();
            c.Set("a", 8);
            c.Set("c", 3);

            var viaStatic = PropertyTreeMerger.Merge(a, b, c);
            var viaExtension = a.Merge(b, c);
            Assert.IsTrue(viaStatic.DeepEquals(viaExtension));
        }
    }
}
