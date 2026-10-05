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
        private const string ReportsMissingScriptsMethodName = "ReportsMissingScripts";
        private const string WindowTypeName = "Mmzkworks.muValidation.Editor.ValidationWindow, works.mmzk.muvalidation.Editor";
        private const string ShowForMethodName = "ShowFor";
        private const string SceneRulesTypeName = "Mmzkworks.muValidation.Editor.SceneRulesRegistry, works.mmzk.muvalidation.Editor";
        private const string SceneRulesMethodName = "GetRulePaths";
        private const string TagRestrictionMethodName = "GetTagRestriction";
        private const string LayerRestrictionMethodName = "GetLayerRestriction";

        private delegate int GetSeverityDelegate(GameObject go, out string message);

        private static GetSeverityDelegate _getSeverity;
        private static Func<string, string[]> _getSceneRulePaths;
        private static Func<GameObject, string, string> _getTagRestriction;
        private static Func<GameObject, int, string> _getLayerRestriction;
        private static Func<bool> _reportsMissingScripts;
        private static Action<GameObject> _showInWindow;
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
        /// True when muValidation includes missing scripts (with child counts) in <see cref="GetSeverity"/>,
        /// so muHierarchy need not search children for them. False if muValidation is not installed.
        /// </summary>
        public static bool ReportsMissingScripts()
        {
            if (!_resolved) Resolve();
            if (_reportsMissingScripts == null) return false;

            try
            {
                return _reportsMissingScripts();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        /// <summary>
        /// True when <see cref="ShowInValidationWindow"/> works (muValidation is installed).
        /// </summary>
        public static bool CanShowInValidationWindow
        {
            get
            {
                if (!_resolved) Resolve();
                return _showInWindow != null;
            }
        }

        /// <summary>
        /// Opens the muValidation Validation window with the GameObject's issues selected.
        /// Does nothing if muValidation is not installed.
        /// </summary>
        public static void ShowInValidationWindow(GameObject go)
        {
            if (!_resolved) Resolve();
            if (_showInWindow == null || go == null) return;

            try
            {
                _showInWindow(go);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
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

        /// <summary>
        /// Returns why the GameObject may not take the tag under the muValidation SceneRules of its scene.
        /// Null if it may, or muValidation is not installed.
        /// </summary>
        public static string GetTagRestriction(GameObject go, string tag)
        {
            if (!_resolved) Resolve();
            if (_getTagRestriction == null || go == null) return null;

            try
            {
                return _getTagRestriction(go, tag);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        /// <summary>
        /// Returns why the GameObject may not move to the layer under the muValidation SceneRules of its scene.
        /// Null if it may, or muValidation is not installed.
        /// </summary>
        public static string GetLayerRestriction(GameObject go, int layer)
        {
            if (!_resolved) Resolve();
            if (_getLayerRestriction == null || go == null) return null;

            try
            {
                return _getLayerRestriction(go, layer);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        private static void Resolve()
        {
            _resolved = true;

            var sceneRulesType = Type.GetType(SceneRulesTypeName);
            var tagRestriction = sceneRulesType?.GetMethod(TagRestrictionMethodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject), typeof(string) }, null);
            if (tagRestriction != null && tagRestriction.ReturnType == typeof(string))
            {
                _getTagRestriction = (Func<GameObject, string, string>)Delegate.CreateDelegate(typeof(Func<GameObject, string, string>), tagRestriction);
            }

            var layerRestriction = sceneRulesType?.GetMethod(LayerRestrictionMethodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject), typeof(int) }, null);
            if (layerRestriction != null && layerRestriction.ReturnType == typeof(string))
            {
                _getLayerRestriction = (Func<GameObject, int, string>)Delegate.CreateDelegate(typeof(Func<GameObject, int, string>), layerRestriction);
            }

            var rulePaths = sceneRulesType?.GetMethod(SceneRulesMethodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (rulePaths != null && rulePaths.ReturnType == typeof(string[]))
            {
                _getSceneRulePaths = (Func<string, string[]>)Delegate.CreateDelegate(typeof(Func<string, string[]>), rulePaths);
            }

            var showFor = Type.GetType(WindowTypeName)?.GetMethod(ShowForMethodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null);
            if (showFor != null && showFor.ReturnType == typeof(void))
            {
                _showInWindow = (Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>), showFor);
            }

            var validationType = Type.GetType(TypeName);
            var reportsMissing = validationType?.GetMethod(ReportsMissingScriptsMethodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (reportsMissing != null && reportsMissing.ReturnType == typeof(bool))
            {
                _reportsMissingScripts = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), reportsMissing);
            }

            var parameterTypes = new[] { typeof(GameObject), typeof(string).MakeByRefType() };
            var method = validationType?.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            if (method == null || method.ReturnType != typeof(int)) return;
            _getSeverity = (GetSeverityDelegate)Delegate.CreateDelegate(typeof(GetSeverityDelegate), method);
        }
    }
}
