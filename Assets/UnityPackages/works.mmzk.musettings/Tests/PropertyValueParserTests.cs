using Mmzkworks.muProperty;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muSettings.Tests
{
    public class PropertyValueParserTests
    {
        [Test]
        public void Parse_BoolIntFloatString()
        {
            Assert.AreEqual(true, PropertyValueParser.Parse("True").AsBool());
            Assert.AreEqual(false, PropertyValueParser.Parse("false").AsBool());
            Assert.AreEqual(12, PropertyValueParser.Parse("12").AsInt());
            Assert.AreEqual(1.5f, PropertyValueParser.Parse("1.5").AsFloat());
            Assert.AreEqual(1f, PropertyValueParser.Parse("1.0").AsFloat());
            Assert.AreEqual("hello", PropertyValueParser.Parse("hello").AsString());
        }

        [Test]
        public void Parse_Vectors()
        {
            var v2 = PropertyValueParser.Parse("1,2").AsVector2();
            Assert.AreEqual(1f, v2.x);
            Assert.AreEqual(2f, v2.y);
            var v3 = PropertyValueParser.Parse("(1, 2, 3)").AsVector3();
            Assert.AreEqual(3f, v3.z);
            var v4 = PropertyValueParser.Parse("[0,1,0,1]").AsVector4();
            Assert.AreEqual(1f, v4.w);
        }

        [Test]
        public void Parse_HexColor()
        {
            var c = PropertyValueParser.Parse("#FF0000").AsColor();
            Assert.AreEqual(1f, c.r);
            Assert.AreEqual(0f, c.g);
            Assert.AreEqual(1f, c.a);
        }

        [Test]
        public void Parse_NonNumericComma_IsString()
        {
            Assert.AreEqual(PropertyValueKind.String, PropertyValueParser.Parse("hello,world").Kind);
        }
    }
}
