using UnityEditor;
using UnityEngine;
#if UNITY_6000_4_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Mmzkworks.muHierarchy.Editor
{
    /// <summary>
    /// Bridges the int instance ID APIs (2022.3 - 6000.3) and the EntityId APIs (6000.4+).
    /// Files that store IDs declare the same ObjectId alias.
    /// </summary>
    internal static class ObjectIdCompat
    {
#if UNITY_6000_4_OR_NEWER
        public static ObjectId GetObjectId(this Object obj) => obj.GetEntityId();
        public static Object ToObject(ObjectId id) => EditorUtility.EntityIdToObject(id);

        public static ObjectId[] SelectedIds
        {
            get => Selection.entityIds;
            set => Selection.entityIds = value;
        }
#else
        public static ObjectId GetObjectId(this Object obj) => obj.GetInstanceID();
        public static Object ToObject(ObjectId id) => EditorUtility.InstanceIDToObject(id);

        public static ObjectId[] SelectedIds
        {
            get => Selection.instanceIDs;
            set => Selection.instanceIDs = value;
        }
#endif
    }
}
