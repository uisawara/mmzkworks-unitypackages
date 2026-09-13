using System;
using Mmzkworks.muProperty;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muSettings.Tests
{
    public class JsonPropertySourceTests
    {
        [Test]
        public void Parse_NestedObjectAndTypes()
        {
            const string json = @"{
  ""render"": {
    ""quality"": 2,
    ""scale"": 1.5,
    ""offset"": [1, 2],
    ""tint"": ""#FF0000FF"",
    ""enabled"": true,
    ""name"": ""main""
  }
}";
            var tree = JsonPropertySource.Parse(json);
            Assert.AreEqual(2, tree.GetInt("render/quality"));
            Assert.AreEqual(1.5f, tree.GetFloat("render/scale"));
            Assert.AreEqual(1f, tree.GetVector2("render/offset").x);
            Assert.AreEqual(2f, tree.GetVector2("render/offset").y);
            Assert.AreEqual(1f, tree.GetColor("render/tint").r);
            Assert.AreEqual(0f, tree.GetColor("render/tint").g);
            Assert.AreEqual(1f, tree.GetColor("render/tint").a);
            Assert.AreEqual(true, tree.GetBool("render/enabled"));
            Assert.AreEqual("main", tree.GetString("render/name"));
        }

        [Test]
        public void Parse_ColorObjectAndVectorObject()
        {
            const string json = @"{
  ""tint"": { ""r"": 0, ""g"": 1, ""b"": 0, ""a"": 1 },
  ""pos"": { ""x"": 1, ""y"": 2, ""z"": 3 }
}";
            var tree = JsonPropertySource.Parse(json);
            Assert.AreEqual(1f, tree.GetColor("tint").g);
            Assert.AreEqual(3f, tree.GetVector3("pos").z);
        }

        [Test]
        public void RoundTrip_PreservesKinds()
        {
            var src = new PropertyTree();
            src.Set("flag", false);
            src.Set("count", 7);
            src.Set("scale", 1f);
            src.Set("v2", new Vector2(1.25f, 2.5f));
            src.Set("v3", new Vector3(1f, 2f, 3f));
            src.Set("v4", new Vector4(1f, 2f, 3f, 4f));
            src.Set("tint", new Color(1f, 0f, 0f, 1f));
            src.Set("name", "quote\"\\");
            src.Set("nested/child", 1);

            var json = JsonPropertySource.ToJson(src);
            var dst = JsonPropertySource.Parse(json);
            Assert.IsTrue(src.DeepEquals(dst), json);
        }

        [Test]
        public void Float_WritesDecimalSoKindSurvives()
        {
            var src = new PropertyTree();
            src.Set("scale", 1f);
            var json = JsonPropertySource.ToJson(src);
            StringAssert.Contains("1.0", json);
            var dst = JsonPropertySource.Parse(json);
            Assert.AreEqual(PropertyValueKind.Float, dst.TryGet("scale", out var v) ? v.Kind : (PropertyValueKind)(-1));
        }

        [Test]
        public void SaveAndLoad_FileRoundTrip()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "musettings-tests",
                Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var src = new PropertyTree();
                src.Set("a/b", 4);
                var io = new JsonPropertySource(path);
                io.Save(src);
                var dst = io.Load();
                Assert.AreEqual(4, dst.GetInt("a/b"));
            }
            finally
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
        }

        [Test]
        public void Parse_RootMustBeObject()
        {
            Assert.Throws<FormatException>(() => JsonPropertySource.Parse("[1, 2]"));
        }

        [Test]
        public void Parse_SkipsNull()
        {
            var tree = JsonPropertySource.Parse(@"{ ""a"": null, ""b"": 1 }");
            Assert.IsFalse(tree.Has("a"));
            Assert.AreEqual(1, tree.GetInt("b"));
        }

        [Test]
        public void Parse_IntVersusFloatTokens()
        {
            var tree = JsonPropertySource.Parse(@"{ ""i"": 2, ""f"": 2.0 }");
            Assert.AreEqual(2, tree.GetInt("i"));
            Assert.AreEqual(2f, tree.GetFloat("f"));
            Assert.AreEqual(0, tree.GetInt("f"));
        }

        [Test]
        public void ToJson_EscapesStrings()
        {
            var tree = new PropertyTree();
            tree.Set("s", "a\"b\\c");
            var json = JsonPropertySource.ToJson(tree);
            StringAssert.Contains("\\\"", json);
            Assert.AreEqual("a\"b\\c", JsonPropertySource.Parse(json).GetString("s"));
        }
    }
}
