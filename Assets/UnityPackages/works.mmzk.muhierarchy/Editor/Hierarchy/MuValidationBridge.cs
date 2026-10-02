using System;
using System.Reflection;
using UnityEngine;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Optional link to muValidation (works.mmzk.muvalidation). Looked up by reflection,
    /// so muHierarchy has no dependency on it and works the same when it is not installed.
    /// </summary>
    internal static class MuValidationBridge
    {
        public enum Severity
        {
            None = 0,
            Warning = 1,
            Error = 2,
        }

        private const string TypeName = "Mmzkworks.muValidation.Editor.SceneValidation, works.mmzk.muvalidation.Editor";
        private const string MethodName = "GetSeverityIncludingChildren";
        private const string SceneRulesTypeName = "Mmzkworks.muValidation.Editor.SceneRulesRegistry, works.mmzk.muvalidation.Editor";
        private const string SceneRulesMethodName = "GetRulePaths";

        private delegate int GetSeverityDelegate(GameObject go, out string message);

        private static GetSeverityDelegate _getSeverity;
        private static Func<string, string[]> _getSceneRulePaths;
        private static bool _resolved;

        /// <summary>
        /// Returns the validation severity of the GameObject and its children, with the tooltip message.
        /// None if valid or muValidation is not installed.
        /// </summary>
        public static Severity GetSeverity(GameObject go, out string message)
        {
            message = null;
            if (!_resolved) Resolve();
            if (_getSeverity == null || go == null) return Severity.None;

            try
            {
                return (Severity)_getSeverity(go, out message);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Severity.None;
            }
        }

        /// <summary>
        /// Returns the asset paths of the muValidation SceneRules that apply to the scene.
        /// Empty if none apply or muValidation is not installed.
        /// </summary>
        public static string[] GetSceneRulePaths(string scenePath)
        {
            if (!_resolved) Resolve();
            if (_getSceneRulePaths == null) return Array.Empty<string>();

            try
            {
                return _getSceneRulePaths(scenePath) ?? Array.Empty<string>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Array.Empty<string>();
            }
        }

        private static void Resolve()
        {
            _resolved = true;

            var rulePaths = Type.GetType(SceneRulesTypeName)?.GetMethod(SceneRulesMethodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (rulePaths != null && rulePaths.ReturnType == typeof(string[]))
            {
                _getSceneRulePaths = (Func<string, string[]>)Delegate.CreateDelegate(typeof(Func<string, string[]>), rulePaths);
            }

            var parameterTypes = new[] { typeof(GameObject), typeof(string).MakeByRefType() };
            var method = Type.GetType(TypeName)?.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            if (method == null || method.ReturnType != typeof(int)) return;
            _getSeverity = (GetSeverityDelegate)Delegate.CreateDelegate(typeof(GetSeverityDelegate), method);
        }
    }
}
