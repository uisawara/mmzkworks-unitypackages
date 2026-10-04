using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Cached Prefab override checks for the Hierarchy rows.
    /// Ignores overrides that Apply cannot clear: an object reference saved as null whose current
    /// value is a DontSave object (e.g. a mesh generated in the editor by an [ExecuteAlways] component).
    /// The cache is cleared on any scene / Prefab change, so a repaint only does a dictionary lookup.
    /// </summary>
    [InitializeOnLoad]
    internal static class PrefabOverrideCache
    {
        private static readonly Dictionary<int, bool> SelfCache = new Dictionary<int, bool>(256);
        private static readonly Dictionary<int, bool> ChildrenCache = new Dictionary<int, bool>(256);

        static PrefabOverrideCache()
        {
            ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream _) => Clear();
            Undo.undoRedoPerformed += Clear;
            EditorApplication.hierarchyChanged += Clear;
            PrefabUtility.prefabInstanceUpdated += _ => Clear();
            EditorSceneManager.sceneOpened += (_, __) => Clear();
            EditorSceneManager.sceneClosed += _ => Clear();
        }

        public static void Clear()
        {
            SelfCache.Clear();
            ChildrenCache.Clear();
        }

        /// <summary>
        /// True if the Prefab instance has overrides that Apply would actually change.
        /// </summary>
        public static bool HasOverrides(GameObject go)
        {
            if (go == null)
                return false;
            int id = go.GetInstanceID();
            if (SelfCache.TryGetValue(id, out bool result))
                return result;
            result = ComputeHasOverrides(go);
            SelfCache[id] = result;
            return result;
        }

        /// <summary>
        /// True if any descendant Prefab instance has overrides that Apply would actually change.
        /// </summary>
        public static bool HasChildOverrides(GameObject go)
        {
            if (go == null)
                return false;
            int id = go.GetInstanceID();
            if (ChildrenCache.TryGetValue(id, out bool result))
                return result;
            result = ComputeHasChildOverrides(go);
            ChildrenCache[id] = result;
            return result;
        }

        private static bool ComputeHasChildOverrides(GameObject go)
        {
            foreach (Transform child in go.transform)
            {
                var childGo = child.gameObject;
                if (!childGo.scene.IsValid() || !childGo.scene.isLoaded)
                    continue;
                if (PrefabUtility.IsPartOfPrefabInstance(childGo) && HasOverrides(childGo))
                    return true;
                if (HasChildOverrides(childGo))
                    return true;
            }
            return false;
        }

        private static bool ComputeHasOverrides(GameObject go)
        {
            // Cheap check first: most rows stop here.
            if (!PrefabUtility.HasPrefabInstanceAnyOverrides(go, false))
                return false;

            if (PrefabUtility.GetAddedComponents(go).Count > 0
                || PrefabUtility.GetRemovedComponents(go).Count > 0
                || PrefabUtility.GetAddedGameObjects(go).Count > 0
                || PrefabUtility.GetRemovedGameObjects(go).Count > 0)
                return true;

            var modifications = PrefabUtility.GetPropertyModifications(go);
            foreach (var objectOverride in PrefabUtility.GetObjectOverrides(go, false))
            {
                var instanceObject = objectOverride.instanceObject;
                if (instanceObject == null)
                    return true;
                if (HasMeaningfulModification(instanceObject, modifications))
                    return true;
            }
            return false;
        }

        private static bool HasMeaningfulModification(Object instanceObject, PropertyModification[] modifications)
        {
            if (modifications == null)
                return true;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceObject);
            if (source == null)
                return true;

            SerializedObject serializedObject = null;
            try
            {
                foreach (var modification in modifications)
                {
                    if (modification.target != source || PrefabUtility.IsDefaultOverride(modification))
                        continue;
                    // Only "saved as null" object references can come from DontSave objects.
                    if (modification.objectReference != null || !string.IsNullOrEmpty(modification.value))
                        return true;

                    serializedObject ??= new SerializedObject(instanceObject);
                    var property = serializedObject.FindProperty(modification.propertyPath);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                        return true;
                    var current = property.objectReferenceValue;
                    if (current == null || (current.hideFlags & HideFlags.DontSaveInEditor) == 0)
                        return true;
                }
                return false;
            }
            finally
            {
                serializedObject?.Dispose();
            }
        }
    }
}
