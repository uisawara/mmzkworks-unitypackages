using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    /// <summary>
    /// Builds a graph of root GameObject -> its components -> objects referenced by their serialized fields (one level).
    /// With includeChildren, descendant GameObjects and their components are added as well.
    /// </summary>
    public static class RefGraphBuilder
    {
        public const string MissingScriptAssemblyName = "(Missing Script)";

        // 参照としてはノイズになるプロパティ（所属 GameObject・Prefab 内部情報・Transform 階層）
        // 子階層は includeChildren で明示的な親子エッジとして表す
        private static readonly HashSet<string> ExcludedProperties = new HashSet<string>
        {
            "m_Script",
            "m_GameObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_CorrespondingSourceObject",
            "m_Father",
            "m_Children",
        };

        public static RefGraph Build(GameObject go, bool includeChildren = false)
        {
            var graph = new RefGraph();
            if (go == null)
                return graph;

            // Root first, then descendants in depth-first (Hierarchy) order
            var gameObjects = new List<GameObject> { go };
            if (includeChildren)
                CollectDescendants(go.transform, gameObjects);

            var gameObjectIds = new Dictionary<GameObject, string>();
            for (int i = 0; i < gameObjects.Count; i++)
            {
                var g = gameObjects[i];
                var id = i == 0 ? RefGraph.RootId : ChildId(i);
                gameObjectIds[g] = id;
                graph.AddNode(new RefGraphNode
                {
                    Id = id,
                    Kind = i == 0 ? RefGraphNodeKind.Root : RefGraphNodeKind.ChildObject,
                    Label = g.name,
                    SubLabel = i == 0
                        ? (EditorUtility.IsPersistent(g) ? "Prefab" : "GameObject")
                        : RelativePath(go.transform, g.transform),
                    Tooltip = DescribeLocation(g),
                    Target = g,
                    Order = i,
                    Depth = GetDepth(go.transform, g.transform),
                });
                if (i > 0)
                    graph.AddEdge(gameObjectIds[g.transform.parent.gameObject], id);
            }

            var componentIds = new Dictionary<Component, string>();
            var ownedComponents = new List<(Component comp, string id)>();
            int componentOrder = 0;
            for (int gi = 0; gi < gameObjects.Count; gi++)
            {
                var g = gameObjects[gi];
                var ownerId = gameObjectIds[g];
                var ownerPrefix = gi == 0 ? null : g.name + " · ";
                var components = g.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    var comp = components[i];
                    var id = gi == 0 ? ComponentId(i) : ChildComponentId(gi, i);
                    if (comp == null)
                    {
                        graph.AddNode(new RefGraphNode
                        {
                            Id = id,
                            Kind = RefGraphNodeKind.MissingScript,
                            Label = "Missing Script",
                            SubLabel = ownerPrefix + MissingScriptAssemblyName,
                            AssemblyName = MissingScriptAssemblyName,
                            OwnerId = ownerId,
                            Order = componentOrder++,
                        });
                    }
                    else
                    {
                        var type = comp.GetType();
                        var assemblyName = type.Assembly.GetName().Name;
                        graph.AddNode(new RefGraphNode
                        {
                            Id = id,
                            Kind = RefGraphNodeKind.Component,
                            Label = type.Name,
                            SubLabel = ownerPrefix + assemblyName,
                            AssemblyName = assemblyName,
                            Tooltip = $"{type.FullName}\n{assemblyName}",
                            Target = comp,
                            OwnerId = ownerId,
                            Order = componentOrder++,
                        });
                        componentIds[comp] = id;
                        ownedComponents.Add((comp, id));
                    }
                    graph.AddEdge(ownerId, id);
                }
            }

            var referenceIds = new Dictionary<Object, string>();
            foreach (var (comp, fromId) in ownedComponents)
            {
                using (var so = new SerializedObject(comp))
                {
                    var it = so.GetIterator();
                    bool enterChildren = true;
                    while (it.Next(enterChildren))
                    {
                        if (it.depth == 0 && ExcludedProperties.Contains(it.name))
                        {
                            enterChildren = false;
                            continue;
                        }
                        enterChildren = it.hasChildren && it.propertyType != SerializedPropertyType.String;

                        if (it.propertyType != SerializedPropertyType.ObjectReference)
                            continue;
                        var target = it.objectReferenceValue;
                        if (target == null)
                            continue;

                        var toId = ResolveTargetId(graph, gameObjectIds, componentIds, referenceIds, target);
                        if (toId == fromId)
                            continue;
                        graph.AddEdge(fromId, toId, FormatFieldPath(it.propertyPath));
                    }
                }
            }

            return graph;
        }

        public static string ComponentId(int index) => "c:" + index;

        public static string ChildId(int gameObjectIndex) => "g:" + gameObjectIndex;

        public static string ChildComponentId(int gameObjectIndex, int componentIndex) =>
            ChildId(gameObjectIndex) + "/c:" + componentIndex;

        /// <summary>"items.Array.data[0].target" -> "items[0].target"</summary>
        public static string FormatFieldPath(string propertyPath)
        {
            return string.IsNullOrEmpty(propertyPath) ? propertyPath : propertyPath.Replace(".Array.data[", "[");
        }

        private static void CollectDescendants(Transform parent, List<GameObject> result)
        {
            foreach (Transform child in parent)
            {
                result.Add(child.gameObject);
                CollectDescendants(child, result);
            }
        }

        private static int GetDepth(Transform root, Transform t)
        {
            int depth = 0;
            for (var p = t; p != null && p != root; p = p.parent)
                depth++;
            return depth;
        }

        /// <summary>Path from (but excluding) root, e.g. "Body/Arm".</summary>
        private static string RelativePath(Transform root, Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        private static string ResolveTargetId(
            RefGraph graph,
            Dictionary<GameObject, string> gameObjectIds,
            Dictionary<Component, string> componentIds,
            Dictionary<Object, string> referenceIds,
            Object target)
        {
            if (target is GameObject g && gameObjectIds.TryGetValue(g, out var goId))
                return goId;
            if (target is Component c && componentIds.TryGetValue(c, out var compId))
                return compId;

            if (referenceIds.TryGetValue(target, out var id))
                return id;
            int order = referenceIds.Count;
            id = "r:" + order;
            referenceIds[target] = id;

            var label = target.name;
            if (string.IsNullOrEmpty(label) && target is Component targetComp && targetComp != null)
                label = targetComp.gameObject.name;
            graph.AddNode(new RefGraphNode
            {
                Id = id,
                Kind = RefGraphNodeKind.Reference,
                Label = string.IsNullOrEmpty(label) ? "(no name)" : label,
                SubLabel = target.GetType().Name,
                Tooltip = DescribeLocation(target),
                Target = target,
                Order = order,
            });
            return id;
        }

        private static string DescribeLocation(Object obj)
        {
            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(assetPath))
                return assetPath;

            Transform t = null;
            if (obj is GameObject g) t = g.transform;
            else if (obj is Component c) t = c.transform;
            if (t == null)
                return obj.GetType().FullName;

            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            if (t.gameObject.scene.IsValid())
                path = t.gameObject.scene.name + ": " + path;
            return path;
        }
    }
}
