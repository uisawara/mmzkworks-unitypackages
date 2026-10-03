using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    /// <summary>Builds a graph of root GameObject -> its components -> objects referenced by their serialized fields (one level).</summary>
    public static class RefGraphBuilder
    {
        public const string MissingScriptAssemblyName = "(Missing Script)";

        // 参照としてはノイズになるプロパティ（所属 GameObject・Prefab 内部情報・Transform 階層）
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

        public static RefGraph Build(GameObject go)
        {
            var graph = new RefGraph();
            if (go == null)
                return graph;

            graph.AddNode(new RefGraphNode
            {
                Id = RefGraph.RootId,
                Kind = RefGraphNodeKind.Root,
                Label = go.name,
                SubLabel = EditorUtility.IsPersistent(go) ? "Prefab" : "GameObject",
                Tooltip = DescribeLocation(go),
                Target = go,
            });

            var components = go.GetComponents<Component>();
            var componentIds = new Dictionary<Component, string>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                var id = ComponentId(i);
                if (comp == null)
                {
                    graph.AddNode(new RefGraphNode
                    {
                        Id = id,
                        Kind = RefGraphNodeKind.MissingScript,
                        Label = "Missing Script",
                        SubLabel = MissingScriptAssemblyName,
                        AssemblyName = MissingScriptAssemblyName,
                        Order = i,
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
                        SubLabel = assemblyName,
                        AssemblyName = assemblyName,
                        Tooltip = $"{type.FullName}\n{assemblyName}",
                        Target = comp,
                        Order = i,
                    });
                    componentIds[comp] = id;
                }
                graph.AddEdge(RefGraph.RootId, id);
            }

            var referenceIds = new Dictionary<Object, string>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null)
                    continue;
                var fromId = componentIds[comp];

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

                        var toId = ResolveTargetId(graph, go, componentIds, referenceIds, target);
                        if (toId == fromId)
                            continue;
                        graph.AddEdge(fromId, toId, FormatFieldPath(it.propertyPath));
                    }
                }
            }

            return graph;
        }

        public static string ComponentId(int index) => "c:" + index;

        /// <summary>"items.Array.data[0].target" -> "items[0].target"</summary>
        public static string FormatFieldPath(string propertyPath)
        {
            return string.IsNullOrEmpty(propertyPath) ? propertyPath : propertyPath.Replace(".Array.data[", "[");
        }

        private static string ResolveTargetId(
            RefGraph graph,
            GameObject root,
            Dictionary<Component, string> componentIds,
            Dictionary<Object, string> referenceIds,
            Object target)
        {
            if (target == root)
                return RefGraph.RootId;
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
