using NUnit.Framework;

namespace Mmzkworks.muAsmdefgraph.Tests
{
    public class AsmdefJsonHelperTests
    {
        [Test]
        public void ExtractStringProperty_ValidName_ReturnsValue()
        {
            var json = "{\"name\":\"Foo\"}";
            var result = AsmdefJsonHelper.ExtractStringProperty(json, "name");
            Assert.AreEqual("Foo", result);
        }

        [Test]
        public void ExtractStringProperty_EmptyJson_ReturnsEmpty()
        {
            var result = AsmdefJsonHelper.ExtractStringProperty("", "name");
            Assert.AreEqual("", result);
        }

        [Test]
        public void ExtractStringProperty_MissingKey_ReturnsEmpty()
        {
            var json = "{\"other\":\"value\"}";
            var result = AsmdefJsonHelper.ExtractStringProperty(json, "name");
            Assert.AreEqual("", result);
        }

        [Test]
        public void ExtractStringProperty_MalformedJson_ReturnsEmpty()
        {
            var json = "{\"name\":";
            var result = AsmdefJsonHelper.ExtractStringProperty(json, "name");
            Assert.AreEqual("", result);
        }

        [Test]
        public void ExtractStringProperty_KeyWithColonInValue_ReturnsValueUpToClosingQuote()
        {
            var json = "{\"name\":\"Foo:Bar\"}";
            var result = AsmdefJsonHelper.ExtractStringProperty(json, "name");
            Assert.AreEqual("Foo:Bar", result);
        }

        [Test]
        public void ExtractStringProperty_AsmdefStyleName_ReturnsValue()
        {
            var json = "{\"name\":\"MyAssembly.Editor\"}";
            var result = AsmdefJsonHelper.ExtractStringProperty(json, "name");
            Assert.AreEqual("MyAssembly.Editor", result);
        }

        [Test]
        public void ExtractStringProperty_FirstKeyInObject_ReturnsValue()
        {
            var json = "{\"name\":\"First\",\"references\":[]}";
            var result = AsmdefJsonHelper.ExtractStringProperty(json, "name");
            Assert.AreEqual("First", result);
        }

        [Test]
        public void ExtractStringArrayProperty_ValidArray_ReturnsList()
        {
            var json = "{\"references\":[\"UnityEngine\",\"UnityEditor\"]}";
            var result = AsmdefJsonHelper.ExtractStringArrayProperty(json, "references");
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("UnityEngine", result[0]);
            Assert.AreEqual("UnityEditor", result[1]);
        }

        [Test]
        public void ExtractStringArrayProperty_EmptyArray_ReturnsEmptyList()
        {
            var json = "{\"references\":[]}";
            var result = AsmdefJsonHelper.ExtractStringArrayProperty(json, "references");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ExtractStringArrayProperty_MissingKey_ReturnsNull()
        {
            var json = "{\"name\":\"Foo\"}";
            var result = AsmdefJsonHelper.ExtractStringArrayProperty(json, "references");
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractStringArrayProperty_SingleElement_ReturnsOneItem()
        {
            var json = "{\"references\":[\"GUID:abc123def456789012345678901234ab\"]}";
            var result = AsmdefJsonHelper.ExtractStringArrayProperty(json, "references");
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("GUID:abc123def456789012345678901234ab", result[0]);
        }

        [Test]
        public void ExtractStringArrayProperty_EmptyStringElements_ExcludedFromResult()
        {
            var json = "{\"references\":[\"\",\"Valid\",\"\"]}";
            var result = AsmdefJsonHelper.ExtractStringArrayProperty(json, "references");
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Valid", result[0]);
        }

        [Test]
        public void ExtractStringArrayProperty_MultipleElementsWithBlanks_ExcludesOnlyEmptyStrings()
        {
            var json = "{\"refs\":[\"A\",\"\",\"B\"]}";
            var result = AsmdefJsonHelper.ExtractStringArrayProperty(json, "refs");
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("A", result[0]);
            Assert.AreEqual("B", result[1]);
        }
    }
}
