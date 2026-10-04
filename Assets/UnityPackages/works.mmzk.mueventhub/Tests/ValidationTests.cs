using System.Collections.Generic;
using System.Reflection;
using Mmzkworks.muValidation;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muEventHub.Tests
{
    // The generic attributes (NotEmpty, RequireComponentInParent, SingleInScene) are tested in muValidation.
    // Here: only the ContactSensor-specific check.
    public class ValidationTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private static ValidationResult RunClassAttributes(Component target)
        {
            var result = new ValidationResult();
            foreach (var attribute in target.GetType().GetCustomAttributes<ValidationAttribute>(true))
            {
                attribute.Validate(new ValidationContext(target, ValidationLocation.Prefab, null, null, attribute.Severity, result));
            }

            return result;
        }

        [Test]
        public void ContactSensor_WithoutColliderOrRigidbody_IsError()
        {
            var go = Create("Gem");
            go.AddComponent<EventActor>();
            var sensor = go.AddComponent<ContactSensor>();

            Assert.AreEqual(1, RunClassAttributes(sensor).Messages.Count);

            go.AddComponent<SphereCollider>();
            Assert.AreEqual(0, RunClassAttributes(sensor).Messages.Count);
        }

        [Test]
        public void ContactSensor_WithRigidbodyOnly_IsValid()
        {
            var go = Create("Gem");
            go.AddComponent<EventActor>();
            go.AddComponent<Rigidbody>();

            Assert.AreEqual(0, RunClassAttributes(go.AddComponent<ContactSensor>()).Messages.Count);
        }
    }
}
