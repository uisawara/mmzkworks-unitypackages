using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmzkworks.muDashboard.Editor
{
    internal static class PrefabCommands
    {
        private const string ConflictUnique = "Unique name";
        private const string ConflictOverwrite = "Overwrite";
        private const string ConflictFail = "Fail";

        public static IEnumerable<ShortcutCommand> Create()
        {
            yield return new ShortcutCommand
            {
                Id = "prefab.create",
                Category = ShortcutCommandCategory.Prefab,
                Name = "Create Prefab",
                Description = "Saves each selected scene GameObject as a prefab (by default into the folder open in the Project window). Selects the new prefab assets.",
                Params = new[]
                {
                    ShortcutCommandParam.Folder("folder", "Folder", "",
                        "Empty = the folder open in the Project window. A folder that does not exist is created")
                        .WithEmptyText("(Project folder)"),
                    ShortcutCommandParam.Text("name", "File Name", "", "Empty = GameObject name. {name} = GameObject name, {index} = 1, 2, …"),
                    ShortcutCommandParam.Choice("conflict", "If Exists", new[] { ConflictUnique, ConflictOverwrite, ConflictFail }),
                    ShortcutCommandParam.Bool("connect", "Connect", true, "Turn the scene object into an instance of the new prefab")
                },
                Run = CreatePrefab
            };

            yield return new ShortcutCommand
            {
                Id = "prefab.variant",
                Category = ShortcutCommandCategory.Prefab,
                Name = "Create Prefab Variant",
                Description = "Creates a variant of each selected prefab asset. Selects the new variants.",
                Params = new[]
                {
                    ShortcutCommandParam.Folder("folder", "Folder", "", "Empty = next to the base prefab"),
                    ShortcutCommandParam.Text("suffix", "Suffix", " Variant")
                },
                Run = CreateVariant
            };

            yield return new ShortcutCommand
            {
                Id = "prefab.instantiate",
                Category = ShortcutCommandCategory.Prefab,
                Name = "Instantiate Prefab",
                Description = "Places the prefab (or each selected prefab asset) in the scene / open prefab. Selects the new instances.",
                Params = new[]
                    {
                        ShortcutCommandParam.Asset<GameObject>("prefab", "Prefab", "Empty = the selected prefab assets"),
                    }
                    .Concat(GameObjectCommands.ParentParams())
                    .Concat(new[]
                    {
                        ShortcutCommandParam.Vector3("position", "Position", "Local position under the parent"),
                    })
                    .ToArray(),
                Run = Instantiate
            };

            yield return new ShortcutCommand
            {
                Id = "prefab.unpack",
                Category = ShortcutCommandCategory.Prefab,
                Name = "Unpack Prefab",
                Description = "Unpacks the prefab instances that contain the selected GameObjects.",
                Params = new[]
                {
                    ShortcutCommandParam.Choice("mode", "Mode", new[] { "Outermost", "Completely" })
                },
                Run = ctx => ForEachInstanceRoot(ctx, (root, c) =>
                    PrefabUtility.UnpackPrefabInstance(root,
                        c.GetString("mode") == "Completely" ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot,
                        InteractionMode.UserAction))
            };

            yield return new ShortcutCommand
            {
                Id = "prefab.apply",
                Category = ShortcutCommandCategory.Prefab,
                Name = "Apply Overrides",
                Description = "Applies all overrides of the prefab instances that contain the selected GameObjects.",
                Run = ctx => ForEachInstanceRoot(ctx, (root, _) => PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction))
            };

            yield return new ShortcutCommand
            {
                Id = "prefab.revert",
                Category = ShortcutCommandCategory.Prefab,
                Name = "Revert Overrides",
                Description = "Reverts all overrides of the prefab instances that contain the selected GameObjects.",
                Run = ctx => ForEachInstanceRoot(ctx, (root, _) => PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction))
            };

            yield return new ShortcutCommand
            {
                Id = "prefab.open",
                Category = ShortcutCommandCategory.Prefab,
                Name = "Open Prefab Mode",
                Description = "Opens the prefab (or the selected prefab asset / instance) in Prefab Mode. Selects its root.",
                Params = new[]
                {
                    ShortcutCommandParam.Asset<GameObject>("prefab", "Prefab", "Empty = the selected prefab asset or instance")
                },
                Run = OpenPrefab
            };

            yield return new ShortcutCommand
            {
                Id = "prefab.close",
                Category = ShortcutCommandCategory.Prefab,
                Name = "Close Prefab Mode",
                Description = "Returns to the scene. Unity asks to save unsaved prefab changes.",
                Run = ClosePrefab
            };
        }

        // ------------------------------------------------------------------ create

        private static bool CreatePrefab(ShortcutCommandContext ctx)
        {
            var targets = ShortcutCommandUtil.TopLevelOnly(ShortcutCommandContext.SelectedSceneObjects());
            if (targets.Count == 0) return ctx.Fail("Select GameObjects in a scene");

            var folderArg = ctx.GetString("folder");
            var folder = string.IsNullOrEmpty(folderArg)
                ? ShortcutCommandUtil.ActiveProjectFolder()
                : ShortcutCommandUtil.NormalizeAssetFolder(folderArg);
            if (folder == null || !folder.StartsWith("Assets")) return ctx.Fail("Folder must be inside Assets/");
            if (!ShortcutCommandUtil.EnsureFolder(folder)) return ctx.Fail($"Cannot create folder: {folder}");

            var conflict = ctx.GetString("conflict");
            var connect = ctx.GetBool("connect");
            var created = new List<Object>();
            for (var i = 0; i < targets.Count; i++)
            {
                var go = targets[i];
                var fileName = ShortcutCommandUtil.SanitizeFileName(ShortcutCommandUtil.ExpandName(ctx.GetString("name"), go.name, i + 1));
                if (string.IsNullOrEmpty(fileName)) return ctx.Fail("File name is empty");

                var path = $"{folder}/{fileName}.prefab";
                if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    if (conflict == ConflictFail) return ctx.Fail($"Already exists: {path}");
                    if (conflict == ConflictUnique) path = AssetDatabase.GenerateUniqueAssetPath(path);
                }

                bool ok;
                var prefab = connect
                    ? PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction, out ok)
                    : PrefabUtility.SaveAsPrefabAsset(go, path, out ok);
                if (!ok || prefab == null) return ctx.Fail($"Could not save {go.name} as a prefab");
                created.Add(prefab);
            }

            ShortcutCommandContext.Select(created);
            return true;
        }

        private static bool CreateVariant(ShortcutCommandContext ctx)
        {
            var bases = ShortcutCommandContext.SelectedPrefabAssets();
            if (bases.Count == 0) return ctx.Fail("Select prefab assets");

            var folderArg = ctx.GetString("folder");
            string folder = null;
            if (!string.IsNullOrEmpty(folderArg))
            {
                folder = ShortcutCommandUtil.NormalizeAssetFolder(folderArg);
                if (folder == null) return ctx.Fail("Folder must be inside Assets/");
                if (!ShortcutCommandUtil.EnsureFolder(folder)) return ctx.Fail($"Cannot create folder: {folder}");
            }

            var suffix = ctx.Item.GetArg("suffix") ?? " Variant"; // not trimmed: the leading space is intended
            var created = new List<Object>();
            foreach (var basePrefab in bases)
            {
                var basePath = AssetDatabase.GetAssetPath(basePrefab);
                var dir = folder ?? Path.GetDirectoryName(basePath)?.Replace('\\', '/');
                var path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{dir}/{ShortcutCommandUtil.SanitizeFileName(basePrefab.name + suffix)}.prefab");

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                try
                {
                    var variant = PrefabUtility.SaveAsPrefabAsset(instance, path, out var ok);
                    if (!ok || variant == null) return ctx.Fail($"Could not create a variant of {basePrefab.name}");
                    created.Add(variant);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            ShortcutCommandContext.Select(created);
            return true;
        }

        // ------------------------------------------------------------------ instances

        private static bool Instantiate(ShortcutCommandContext ctx)
        {
            if (!ctx.TryGetAsset<GameObject>("prefab", out var fixedPrefab)) return false;
            var prefabs = fixedPrefab != null
                ? new List<GameObject> { fixedPrefab }
                : ShortcutCommandContext.SelectedPrefabAssets();
            if (prefabs.Count == 0) return ctx.Fail("Set a prefab or select prefab assets");
            if (prefabs.Any(p => PrefabUtility.GetPrefabAssetType(p) == PrefabAssetType.NotAPrefab))
                return ctx.Fail("Not a prefab asset");

            if (!GameObjectCommands.TryResolveParent(ctx, out var parent)) return false;
            var position = ctx.GetVector3("position");

            var created = new List<Object>();
            foreach (var prefab in prefabs)
            {
                GameObject instance;
                if (parent != null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                }
                else
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    StageUtility.PlaceGameObjectInCurrentStage(instance);
                }
                if (instance == null) return ctx.Fail($"Could not instantiate {prefab.name}");

                instance.transform.localPosition = position;
                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");
                created.Add(instance);
            }

            ShortcutCommandContext.Select(created);
            return true;
        }

        /// <summary>Runs <paramref name="action"/> once per outermost prefab instance among the selected GameObjects.</summary>
        private static bool ForEachInstanceRoot(ShortcutCommandContext ctx, System.Action<GameObject, ShortcutCommandContext> action)
        {
            var selected = ShortcutCommandContext.SelectedSceneObjects();
            if (selected.Count == 0) return ctx.Fail("Select prefab instances in a scene");

            var roots = selected
                .Select(PrefabUtility.GetOutermostPrefabInstanceRoot)
                .Where(r => r != null)
                .Distinct()
                .ToList();
            if (roots.Count == 0) return ctx.Fail("The selection has no prefab instances");

            foreach (var root in roots) action(root, ctx);

            // Selection is kept, minus anything that no longer exists.
            ShortcutCommandContext.Select(selected.Cast<Object>());
            return true;
        }

        // ------------------------------------------------------------------ prefab mode

        private static bool OpenPrefab(ShortcutCommandContext ctx)
        {
            if (!ctx.TryGetAsset<GameObject>("prefab", out var prefab)) return false;
            if (prefab == null)
            {
                prefab = ShortcutCommandContext.SelectedPrefabAssets().FirstOrDefault();
                if (prefab == null)
                {
                    var instanceRoot = ShortcutCommandContext.SelectedSceneObjects()
                        .Select(PrefabUtility.GetOutermostPrefabInstanceRoot)
                        .FirstOrDefault(r => r != null);
                    if (instanceRoot != null) prefab = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                }
            }
            if (prefab == null) return ctx.Fail("Set a prefab or select a prefab asset / instance");

            var path = AssetDatabase.GetAssetPath(prefab);
            var stage = PrefabStageUtility.OpenPrefab(path);
            if (stage == null) return ctx.Fail($"Could not open {path}");

            ShortcutCommandContext.Select(new Object[] { stage.prefabContentsRoot });
            return true;
        }

        private static bool ClosePrefab(ShortcutCommandContext ctx)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() == null) return true;
            StageUtility.GoToMainStage();
            return PrefabStageUtility.GetCurrentPrefabStage() == null || ctx.Fail("Prefab Mode was not closed");
        }
    }
}
