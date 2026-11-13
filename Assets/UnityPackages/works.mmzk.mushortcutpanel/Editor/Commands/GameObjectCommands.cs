using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmzkworks.muShortcutPanel.Editor
{
    internal static class GameObjectCommands
    {
        private const string ParentNone = "None";
        private const string ParentSelected = "Selected";
        private const string ParentPath = "Path";

        private static readonly string[] PrimitiveNames = { "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad" };

        public static IEnumerable<ShortcutCommand> Create()
        {
            yield return new ShortcutCommand
            {
                Id = "go.createEmpty",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Create Empty",
                Description = "Creates an empty GameObject and selects it.",
                Params = new[] { ShortcutCommandParam.Text("name", "Name", "GameObject") }.Concat(ParentParams()).ToArray(),
                Run = ctx => CreateObject(ctx, () => new GameObject())
            };

            yield return new ShortcutCommand
            {
                Id = "go.createPrimitive",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Create Primitive",
                Description = "Creates a primitive (Cube, Sphere, …) and selects it.",
                Params = new[]
                    {
                        ShortcutCommandParam.Choice("type", "Type", PrimitiveNames),
                        ShortcutCommandParam.Text("name", "Name", "", "Empty = the primitive type"),
                    }
                    .Concat(ParentParams())
                    .ToArray(),
                Run = ctx =>
                {
                    var index = System.Array.IndexOf(PrimitiveNames, ctx.GetString("type"));
                    var type = index >= 0 ? (PrimitiveType)System.Enum.Parse(typeof(PrimitiveType), PrimitiveNames[index]) : PrimitiveType.Cube;
                    return CreateObject(ctx, () => GameObject.CreatePrimitive(type));
                }
            };

            yield return new ShortcutCommand
            {
                Id = "go.find",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Select GameObjects",
                Description = "Selects the GameObjects that match. Fails when nothing matches. In Prefab Mode it searches the open prefab.",
                Params = new[]
                {
                    ShortcutCommandParam.Choice("by", "Find By", new[] { "Path", "Name", "Tag", "Component" }),
                    ShortcutCommandParam.Text("value", "Value", "", "Path: Env/Trees/Oak, Name: Oak, Tag: Player, Component: Rigidbody"),
                    ShortcutCommandParam.Choice("scope", "Scope", new[] { "Active Scene", "All Loaded Scenes" })
                },
                Run = Find
            };

            yield return new ShortcutCommand
            {
                Id = "go.selectRelative",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Select Relative",
                Description = "Selects the parents, direct children or roots of the selected GameObjects.",
                Params = new[] { ShortcutCommandParam.Choice("target", "Target", new[] { "Parent", "Children", "Root" }) },
                Run = SelectRelative
            };

            yield return new ShortcutCommand
            {
                Id = "go.rename",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Rename",
                Description = "Renames the selected GameObjects.",
                Params = new[] { ShortcutCommandParam.Text("pattern", "Name", "{name}", "{name} = current name, {index} = 1, 2, … in selection order") },
                Run = ctx => ForEachSelected(ctx, (go, i) =>
                {
                    Undo.RecordObject(go, "Rename");
                    go.name = ShortcutCommandUtil.ExpandName(ctx.GetString("pattern"), go.name, i + 1);
                    return true;
                })
            };

            yield return new ShortcutCommand
            {
                Id = "go.setParent",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Set Parent",
                Description = "Moves the selected GameObjects under another GameObject (or to the root).",
                Params = new[]
                {
                    ShortcutCommandParam.Text("path", "Parent Path", "", "Hierarchy path, e.g. Env/Trees. Empty = scene root"),
                    ShortcutCommandParam.Bool("worldStays", "Keep World", true, "Keep the world position / rotation / scale")
                },
                Run = SetParent
            };

            yield return new ShortcutCommand
            {
                Id = "go.addComponent",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Add Component",
                Description = "Adds a component to the selected GameObjects.",
                Params = new[] { ShortcutCommandParam.TypeName("type", "Type", "Rigidbody, BoxCollider, MyNamespace.MyBehaviour, …") },
                Run = AddComponent
            };

            yield return new ShortcutCommand
            {
                Id = "go.removeComponent",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Remove Component",
                Description = "Removes every component of the type from the selected GameObjects (objects without one are skipped).",
                Params = new[] { ShortcutCommandParam.TypeName("type", "Type") },
                Run = RemoveComponent
            };

            yield return new ShortcutCommand
            {
                Id = "go.resetTransform",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Reset Transform",
                Description = "Resets the local position / rotation / scale of the selected GameObjects.",
                Params = new[] { ShortcutCommandParam.Choice("what", "Reset", new[] { "All", "Position", "Rotation", "Scale" }) },
                Run = ctx => ForEachSelected(ctx, (go, _) =>
                {
                    var t = go.transform;
                    Undo.RecordObject(t, "Reset Transform");
                    var what = ctx.GetString("what");
                    if (what == "All" || what == "Position") t.localPosition = Vector3.zero;
                    if (what == "All" || what == "Rotation") t.localRotation = Quaternion.identity;
                    if (what == "All" || what == "Scale") t.localScale = Vector3.one;
                    return true;
                })
            };

            yield return new ShortcutCommand
            {
                Id = "go.setActive",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Set Active",
                Description = "Activates or deactivates the selected GameObjects.",
                Params = new[] { ShortcutCommandParam.Bool("active", "Active", true) },
                Run = ctx => ForEachSelected(ctx, (go, _) =>
                {
                    Undo.RecordObject(go, "Set Active");
                    go.SetActive(ctx.GetBool("active"));
                    return true;
                })
            };

            yield return new ShortcutCommand
            {
                Id = "go.duplicate",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Duplicate",
                Description = "Duplicates the selected GameObjects (like Edit > Duplicate) and selects the copies.",
                Run = Duplicate
            };

            yield return new ShortcutCommand
            {
                Id = "go.delete",
                Category = ShortcutCommandCategory.GameObject,
                Name = "Delete",
                Description = "Deletes the selected GameObjects.",
                Run = Delete
            };
        }

        // ------------------------------------------------------------------ parent (shared with prefab.instantiate)

        /// <summary>"Parent: None / Selected / Path" plus the path field.</summary>
        public static IEnumerable<ShortcutCommandParam> ParentParams()
        {
            yield return ShortcutCommandParam.Choice("parent", "Parent", new[] { ParentNone, ParentSelected, ParentPath },
                ParentNone, "None = scene root (or the open prefab's root), Selected = the first selected GameObject");
            yield return ShortcutCommandParam.Text("parentPath", "Parent Path", "", "Hierarchy path, e.g. Env/Trees")
                .When("parent", ParentPath);
        }

        /// <summary>Resolves the parent parameters. <paramref name="parent"/> is null for "None" (root of the current stage).</summary>
        public static bool TryResolveParent(ShortcutCommandContext ctx, out Transform parent)
        {
            parent = null;
            switch (ctx.GetString("parent"))
            {
                case ParentSelected:
                {
                    var selected = ShortcutCommandContext.SelectedSceneObjects().FirstOrDefault();
                    if (selected == null) return ctx.Fail("Parent = Selected, but no GameObject is selected");
                    parent = selected.transform;
                    return true;
                }
                case ParentPath:
                {
                    var path = ctx.GetString("parentPath");
                    if (string.IsNullOrEmpty(path)) return ctx.Fail("Parent path is empty");
                    var found = ShortcutCommandUtil.FindByPath(ShortcutCommandUtil.SearchRoots(allScenes: true), path).FirstOrDefault();
                    if (found == null) return ctx.Fail($"Parent not found: {path}");
                    parent = found.transform;
                    return true;
                }
                default:
                {
                    // In Prefab Mode, "root" means under the prefab's root (a prefab has a single root).
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null) parent = stage.prefabContentsRoot.transform;
                    return true;
                }
            }
        }

        // ------------------------------------------------------------------ commands

        private static bool CreateObject(ShortcutCommandContext ctx, System.Func<GameObject> factory)
        {
            if (!TryResolveParent(ctx, out var parent)) return false;

            var go = factory();
            var name = ctx.GetString("name");
            if (!string.IsNullOrEmpty(name)) go.name = name;

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                // Match what the Hierarchy's "Create" does: take the parent's layer.
                go.layer = parent.gameObject.layer;
            }
            else
            {
                StageUtility.PlaceGameObjectInCurrentStage(go);
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            ShortcutCommandContext.Select(new Object[] { go });
            return true;
        }

        private static bool Find(ShortcutCommandContext ctx)
        {
            var value = ctx.GetString("value");
            if (string.IsNullOrEmpty(value)) return ctx.Fail("Value is empty");

            var roots = ShortcutCommandUtil.SearchRoots(ctx.GetString("scope") == "All Loaded Scenes");
            List<GameObject> found;
            switch (ctx.GetString("by"))
            {
                case "Name":
                    found = ShortcutCommandUtil.AllUnder(roots).Where(g => g.name == value).ToList();
                    break;
                case "Tag":
                    try
                    {
                        found = ShortcutCommandUtil.AllUnder(roots).Where(g => g.CompareTag(value)).ToList();
                    }
                    catch (UnityException)
                    {
                        return ctx.Fail($"Tag is not defined: {value}");
                    }
                    break;
                case "Component":
                {
                    var type = ShortcutCommandUtil.ResolveComponentType(value, out var error);
                    if (type == null) return ctx.Fail(error);
                    found = ShortcutCommandUtil.AllUnder(roots).Where(g => g.GetComponent(type) != null).ToList();
                    break;
                }
                default:
                    found = ShortcutCommandUtil.FindByPath(roots, value);
                    break;
            }

            if (found.Count == 0) return ctx.Fail($"No GameObject matches \"{value}\"");
            ShortcutCommandContext.Select(found.Cast<Object>());
            return true;
        }

        private static bool SelectRelative(ShortcutCommandContext ctx)
        {
            var selected = ShortcutCommandContext.SelectedSceneObjects();
            if (selected.Count == 0) return ctx.Fail("Select GameObjects in a scene");

            IEnumerable<GameObject> result;
            switch (ctx.GetString("target"))
            {
                case "Children":
                    result = selected.SelectMany(g => g.transform.Cast<Transform>().Select(t => t.gameObject));
                    break;
                case "Root":
                    result = selected.Select(g => g.transform.root.gameObject);
                    break;
                default:
                    result = selected.Where(g => g.transform.parent != null).Select(g => g.transform.parent.gameObject);
                    break;
            }

            var list = result.Distinct().ToList();
            if (list.Count == 0) return ctx.Fail("Nothing to select");
            ShortcutCommandContext.Select(list.Cast<Object>());
            return true;
        }

        private static bool SetParent(ShortcutCommandContext ctx)
        {
            var targets = ShortcutCommandUtil.TopLevelOnly(ShortcutCommandContext.SelectedSceneObjects());
            if (targets.Count == 0) return ctx.Fail("Select GameObjects in a scene");

            Transform parent = null;
            var path = ctx.GetString("path");
            if (!string.IsNullOrEmpty(path))
            {
                var found = ShortcutCommandUtil.FindByPath(ShortcutCommandUtil.SearchRoots(allScenes: true), path).FirstOrDefault();
                if (found == null) return ctx.Fail($"Parent not found: {path}");
                parent = found.transform;
            }
            else
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null) parent = stage.prefabContentsRoot.transform;
            }

            foreach (var go in targets)
            {
                if (parent != null && (parent == go.transform || parent.IsChildOf(go.transform)))
                    return ctx.Fail($"Cannot move {go.name} under itself");
            }

            var worldStays = ctx.GetBool("worldStays");
            foreach (var go in targets) Undo.SetTransformParent(go.transform, parent, worldStays, "Set Parent");

            ShortcutCommandContext.Select(targets.Cast<Object>());
            return true;
        }

        private static bool AddComponent(ShortcutCommandContext ctx)
        {
            var type = ShortcutCommandUtil.ResolveComponentType(ctx.GetString("type"), out var error);
            if (type == null) return ctx.Fail(error);

            return ForEachSelected(ctx, (go, _) =>
            {
                var added = Undo.AddComponent(go, type);
                return added != null || ctx.Fail($"Could not add {type.Name} to {go.name}");
            });
        }

        private static bool RemoveComponent(ShortcutCommandContext ctx)
        {
            var type = ShortcutCommandUtil.ResolveComponentType(ctx.GetString("type"), out var error);
            if (type == null) return ctx.Fail(error);
            if (typeof(Transform).IsAssignableFrom(type)) return ctx.Fail("Transform cannot be removed");

            return ForEachSelected(ctx, (go, _) =>
            {
                foreach (var c in go.GetComponents(type)) Undo.DestroyObjectImmediate(c);
                return true;
            });
        }

        private static bool Duplicate(ShortcutCommandContext ctx)
        {
            var targets = ShortcutCommandContext.SelectedSceneObjects();
            if (targets.Count == 0) return ctx.Fail("Select GameObjects in a scene");

            // Same as Edit > Duplicate: keeps prefab links, records Undo and selects the copies.
            Selection.objects = targets.Cast<Object>().ToArray();
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            return true;
        }

        private static bool Delete(ShortcutCommandContext ctx)
        {
            var targets = ShortcutCommandUtil.TopLevelOnly(ShortcutCommandContext.SelectedSceneObjects());
            if (targets.Count == 0) return ctx.Fail("Select GameObjects in a scene");

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && targets.Contains(stage.prefabContentsRoot)) return ctx.Fail("The prefab root cannot be deleted");

            foreach (var go in targets) Undo.DestroyObjectImmediate(go);
            Selection.objects = System.Array.Empty<Object>();
            return true;
        }

        /// <summary>Runs <paramref name="action"/> for each selected scene GameObject; keeps them selected.</summary>
        private static bool ForEachSelected(ShortcutCommandContext ctx, System.Func<GameObject, int, bool> action)
        {
            var targets = ShortcutCommandContext.SelectedSceneObjects();
            if (targets.Count == 0) return ctx.Fail("Select GameObjects in a scene");

            for (var i = 0; i < targets.Count; i++)
            {
                if (!action(targets[i], i)) return false;
            }

            ShortcutCommandContext.Select(targets.Cast<Object>());
            return true;
        }
    }
}
