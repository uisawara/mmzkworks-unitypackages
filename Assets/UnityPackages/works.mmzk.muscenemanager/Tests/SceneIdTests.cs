using System;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muSceneManager.Tests
{
    public class SceneIdTests
    {
        [Serializable]
        private class Holder
        {
            public SceneId Scene;
        }

        [Test]
        public void Constructor_Sets_Name()
        {
            var id = new SceneId("Title");

            Assert.AreEqual("Title", id.Name);
            Assert.IsTrue(id.IsValid);
            Assert.AreEqual("Title", id.ToString());
        }

        [Test]
        public void Constructor_Rejects_Null_Or_Empty()
        {
            Assert.Throws<ArgumentException>(() => new SceneId(null));
            Assert.Throws<ArgumentException>(() => new SceneId(string.Empty));
        }

        [Test]
        public void Default_Is_Invalid()
        {
            var id = default(SceneId);

            Assert.IsFalse(id.IsValid);
            Assert.IsNull(id.Name);
            Assert.AreEqual("(none)", id.ToString());
        }

        [Test]
        public void Same_Name_Is_Equal()
        {
            var a = new SceneId("Title");
            var b = new SceneId("Title");

            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Different_Name_Is_Not_Equal()
        {
            Assert.IsTrue(new SceneId("Title") != new SceneId("Game"));
            // Comparison is case-sensitive
            Assert.IsTrue(new SceneId("Title") != new SceneId("title"));
            Assert.IsTrue(new SceneId("Title") != default);
        }

        [Test]
        public void Default_Equals_Default()
        {
            Assert.IsTrue(default(SceneId) == default(SceneId));
            Assert.AreEqual(default(SceneId).GetHashCode(), default(SceneId).GetHashCode());
        }

        [Test]
        public void Is_Serialized_By_Unity()
        {
            var json = JsonUtility.ToJson(new Holder { Scene = new SceneId("Title") });
            var restored = JsonUtility.FromJson<Holder>(json);

            Assert.AreEqual(new SceneId("Title"), restored.Scene);
        }

        [Test]
        public void Empty_Serialized_Value_Is_Invalid()
        {
            var restored = JsonUtility.FromJson<Holder>("{\"Scene\":{\"_name\":\"\"}}");

            Assert.IsFalse(restored.Scene.IsValid);
            Assert.AreEqual(default(SceneId), restored.Scene);
        }
    }
}
