#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muValidation.Tests
{
    /// <summary>
    /// muHierarchy looks up these Editor methods by name (MuValidationBridge), without referencing muValidation.
    /// The names here must match the bridge, so a rename here is caught instead of silently turning the link off.
    /// </summary>
    public class MuHierarchyContractTests
    {
        private const string SceneValidationType = "Mmzkworks.muValidation.Editor.SceneValidation, works.mmzk.muvalidation.Editor";
        private const string SceneRulesRegistryType = "Mmzkworks.muValidation.Editor.SceneRulesRegistry, works.mmzk.muvalidation.Editor";
        private const string ValidationWindowType = "Mmzkworks.muValidation.Editor.ValidationWindow, works.mmzk.muvalidation.Editor";

        private static void AssertMethod(string typeName, string methodName, Type returnType, params Type[] parameterTypes)
        {
            var type = Type.GetType(typeName);
            Assert.IsNotNull(type, typeName);

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            Assert.IsNotNull(method, $"{typeName}.{methodName}");
            Assert.AreEqual(returnType, method.ReturnType, $"{typeName}.{methodName}");
        }

        [Test]
        public void SceneValidation_GetSeverityIncludingChildren()
        {
            AssertMethod(SceneValidationType, "GetSeverityIncludingChildren", typeof(int), typeof(GameObject), typeof(string).MakeByRefType());
        }

        [Test]
        public void SceneValidation_ReportsMissingScripts()
        {
            AssertMethod(SceneValidationType, "ReportsMissingScripts", typeof(bool));
        }

        [Test]
        public void SceneRulesRegistry_GetRulePaths()
        {
            AssertMethod(SceneRulesRegistryType, "GetRulePaths", typeof(string[]), typeof(string));
        }

        [Test]
        public void SceneRulesRegistry_GetTagRestriction()
        {
            AssertMethod(SceneRulesRegistryType, "GetTagRestriction", typeof(string), typeof(GameObject), typeof(string));
        }

        [Test]
        public void SceneRulesRegistry_GetLayerRestriction()
        {
            AssertMethod(SceneRulesRegistryType, "GetLayerRestriction", typeof(string), typeof(GameObject), typeof(int));
        }

        [Test]
        public void ValidationWindow_ShowFor()
        {
            AssertMethod(ValidationWindowType, "ShowFor", typeof(void), typeof(GameObject));
        }
    }
}
#endif
