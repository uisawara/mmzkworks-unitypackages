using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muValidation.Tests
{
    public class AttributeTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject Create(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent);
            _created.Add(go);
            return go;
        }

        private static ValidationResult RunClass(Component target, ValidationLocation location)
        {
            var result = new ValidationResult();
            foreach (var attribute in target.GetType().GetCustomAttributes<ValidationAttribute>(true))
            {
                attribute.Validate(new ValidationContext(target, location, null, null, attribute.Severity, result));
            }

            return result;
        }

        private static ValidationResult RunField(Component target, string fieldName, object value)
        {
            var result = new ValidationResult();
            var field = target.GetType().GetField(fieldName);
            foreach (var attribute in field.GetCustomAttributes<ValidationAttribute>(true))
            {
                attribute.Validate(new ValidationContext(target, ValidationLocation.Prefab, field, value, attribute.Severity, result));
            }

            return result;
        }

        [Test]
        public void NotEmpty_ReportsNullEmptyAndWhitespace()
        {
            var named = Create("TestNamedComponent").AddComponent<TestNamedComponent>();

            Assert.AreEqual(ValidationSeverity.Error, RunField(named, nameof(TestNamedComponent.label), null).Severity);
            Assert.AreEqual(ValidationSeverity.Error, RunField(named, nameof(TestNamedComponent.label), "").Severity);
            Assert.AreEqual(ValidationSeverity.Error, RunField(named, nameof(TestNamedComponent.label), "  ").Severity);
            Assert.AreEqual(ValidationSeverity.None, RunField(named, nameof(TestNamedComponent.label), "Gem").Severity);
        }

        [Test]
        public void NotEmpty_ChecksEachElement()
        {
            var named = Create("TestNamedComponent").AddComponent<TestNamedComponent>();

            var result = RunField(named, nameof(TestNamedComponent.labels), new List<string> { "a", "", "b", null });

            Assert.AreEqual(2, result.Messages.Count);
            StringAssert.StartsWith("[1]", result.Messages[0].Text);
            StringAssert.StartsWith("[3]", result.Messages[1].Text);
        }

        [Test]
        public void RequireComponentInParent_AcceptsSelfAndParents()
        {
            var root = Create("Root");
            var child = Create("Child", root.transform);
            var part = child.AddComponent<TestPartComponent>();

            Assert.AreEqual(ValidationSeverity.Error, RunClass(part, ValidationLocation.Prefab).Severity);

            root.AddComponent<TestOwnerComponent>();
            Assert.AreEqual(0, RunClass(part, ValidationLocation.Prefab).Messages.Count);

            var self = Create("Self");
            self.AddComponent<TestOwnerComponent>();
            Assert.AreEqual(0, RunClass(self.AddComponent<TestPartComponent>(), ValidationLocation.Prefab).Messages.Count);
        }

        [Test]
        public void RequireComponentInParent_FindsInactiveParents()
        {
            var root = Create("Root");
            root.AddComponent<TestOwnerComponent>();
            root.SetActive(false);
            var part = Create("Child", root.transform).AddComponent<TestPartComponent>();

            Assert.AreEqual(0, RunClass(part, ValidationLocation.Prefab).Messages.Count);
        }

        [Test]
        public void SingleInScene_ReportsDuplicatesIncludingInactive()
        {
            var first = Create("First").AddComponent<TestSingletonComponent>();
            Assert.AreEqual(0, RunClass(first, ValidationLocation.Scene).Messages.Count);

            var second = Create("Second");
            second.AddComponent<TestSingletonComponent>();
            second.SetActive(false);
            Assert.AreEqual(ValidationSeverity.Error, RunClass(first, ValidationLocation.Scene).Severity);
        }

        [Test]
        public void SingleInScene_IsSkippedOutsideScenes()
        {
            var first = Create("First").AddComponent<TestSingletonComponent>();
            Create("Second").AddComponent<TestSingletonComponent>();

            Assert.AreEqual(0, RunClass(first, ValidationLocation.Prefab).Messages.Count);
        }
    }
}
