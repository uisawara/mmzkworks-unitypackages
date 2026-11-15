using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mmzkworks.muDashboard.Editor
{
    internal static class SceneAssetCommands
    {
        public static IEnumerable<ShortcutCommand> Create()
        {
            // ---------------------------------------------------------------- scene

            yield return new ShortcutCommand
            {
                Id = "scene.open",
                Category = ShortcutCommandCategory.Scene,
                Name = "Open Scene",
                Description = "Opens the scene (or the selected scene asset). Single mode asks to save modified scenes first; Cancel stops the action.",
                Params = new[]
                {
                    ShortcutCommandParam.Asset<SceneAsset>("scene", "Scene", "Empty = the selected scene asset"),
                    ShortcutCommandParam.Choice("mode", "Mode", new[] { "Single", "Additive" })
                },
                Run = OpenScene
            };

            yield return new ShortcutCommand
            {
                Id = "scene.new",
                Category = ShortcutCommandCategory.Scene,
                Name = "New Scene",
                Description = "Creates a new untitled scene. Single mode asks to save modified scenes first; Cancel stops the action.",
                Params = new[]
                {
                    ShortcutCommandParam.Choice("setup", "Setup", new[] { "Default", "Empty" }, null, "Default = Main Camera and Directional Light"),
                    ShortcutCommandParam.Choice("mode", "Mode", new[] { "Single", "Additive" })
                },
                Run = NewScene
            };

            yield return new ShortcutCommand
            {
                Id = "scene.save",
                Category = ShortcutCommandCategory.Scene,
                Name = "Save Scene",
                Description = "Saves the active scene or every open scene. An untitled scene asks for a file name; Cancel stops the action.",
                Params = new[] { ShortcutCommandParam.Choice("target", "Target", new[] { "Active Scene", "All Open Scenes" }) },
                Run = SaveScene
            };

            // ---------------------------------------------------------------- asset

            yield return new ShortcutCommand
            {
                Id = "asset.createFolder",
                Category = ShortcutCommandCategory.Asset,
                Name = "Create Folder",
                Description = "Creates the folder (and missing parents) and selects it. An existing folder is just selected.",
                Params = new[] { ShortcutCommandParam.Folder("path", "Path", "Assets/NewFolder") },
                Run = CreateFolder
            };

            yield return new ShortcutCommand
            {
                Id = "asset.duplicate",
                Category = ShortcutCommandCategory.Asset,
                Name = "Duplicate Asset",
                Description = "Duplicates the selected assets next to the originals and selects the copies.",
                Run = DuplicateAssets
            };

            yield return new ShortcutCommand
            {
                Id = "asset.move",
                Category = ShortcutCommandCategory.Asset,
                Name = "Move Asset",
                Description = "Moves the selected assets into the folder (created when missing). Name clashes get a unique name.",
                Params = new[] { ShortcutCommandParam.Folder("folder", "Folder", "Assets") },
                Run = MoveAssets
            };

            yield return new ShortcutCommand
            {
                Id = "asset.save",
                Category = ShortcutCommandCategory.Asset,
                Name = "Save Assets",
                Description = "Writes modified assets to disk and refreshes the Asset Database.",
                Run = _ =>
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    return true;
                }
            };
        }

        // ------------------------------------------------------------------ scene

        private static bool OpenScene(ShortcutCommandContext ctx)
        {
            if (!ctx.TryGetAsset<SceneAsset>("scene", out var scene)) return false;
            scene ??= ShortcutCommandContext.SelectedAssets().OfType<SceneAsset>().FirstOrDefault();
            if (scene == null) return ctx.Fail("Set a scene or select a scene asset");

            var additive = ctx.GetString("mode") == "Additive";
            if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return ctx.Fail("Canceled");

            var path = AssetDatabase.GetAssetPath(scene);
            var opened = EditorSceneManager.OpenScene(path, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            return opened.IsValid() || ctx.Fail($"Could not open {path}");
        }

        private static bool NewScene(ShortcutCommandContext ctx)
        {
            var additive = ctx.GetString("mode") == "Additive";
            if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return ctx.Fail("Canceled");

            var setup = ctx.GetString("setup") == "Empty" ? NewSceneSetup.EmptyScene : NewSceneSetup.DefaultGameObjects;
            var scene = EditorSceneManager.NewScene(setup, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            if (!scene.IsValid()) return ctx.Fail("Could not create a scene");
            if (additive) SceneManager.SetActiveScene(scene);
            return true;
        }

        private static bool SaveScene(ShortcutCommandContext ctx)
        {
            var ok = ctx.GetString("target") == "All Open Scenes"
                ? EditorSceneManager.SaveOpenScenes()
                : EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            return ok || ctx.Fail("Scene was not saved");
        }

        // ------------------------------------------------------------------ asset

        private static bool CreateFolder(ShortcutCommandContext ctx)
        {
            var folder = ShortcutCommandUtil.NormalizeAssetFolder(ctx.GetString("path"));
            if (folder == null || !folder.StartsWith("Assets")) return ctx.Fail("Path must be inside Assets/");
            if (!ShortcutCommandUtil.EnsureFolder(folder)) return ctx.Fail($"Cannot create folder: {folder}");

            ShortcutCommandContext.Select(new[] { AssetDatabase.LoadAssetAtPath<Object>(folder) });
            return true;
        }

        private static bool DuplicateAssets(ShortcutCommandContext ctx)
        {
            var assets = SelectedMainAssets();
            if (assets.Count == 0) return ctx.Fail("Select assets in the Project window");

            var copies = new List<Object>();
            foreach (var path in assets)
            {
                var copyPath = AssetDatabase.GenerateUniqueAssetPath(path);
                if (!AssetDatabase.CopyAsset(path, copyPath)) return ctx.Fail($"Could not duplicate {path}");
                copies.Add(AssetDatabase.LoadMainAssetAtPath(copyPath));
            }

            ShortcutCommandContext.Select(copies);
            return true;
        }

        private static bool MoveAssets(ShortcutCommandContext ctx)
        {
            var assets = SelectedMainAssets();
            if (assets.Count == 0) return ctx.Fail("Select assets in the Project window");

            var folder = ShortcutCommandUtil.NormalizeAssetFolder(ctx.GetString("folder"));
            if (folder == null || !folder.StartsWith("Assets")) return ctx.Fail("Folder must be inside Assets/");
            if (!ShortcutCommandUtil.EnsureFolder(folder)) return ctx.Fail($"Cannot create folder: {folder}");

            var moved = new List<Object>();
            foreach (var path in assets)
            {
                if (folder == path || folder.StartsWith(path + "/")) return ctx.Fail($"Cannot move {path} into itself");
                var target = $"{folder}/{Path.GetFileName(path)}";
                if (target != path)
                {
                    target = AssetDatabase.GenerateUniqueAssetPath(target);
                    var error = AssetDatabase.MoveAsset(path, target);
                    if (!string.IsNullOrEmpty(error)) return ctx.Fail(error);
                }
                moved.Add(AssetDatabase.LoadMainAssetAtPath(target));
            }

            ShortcutCommandContext.Select(moved);
            return true;
        }

        /// <summary>Paths of the selected assets (main assets only, each once).</summary>
        private static List<string> SelectedMainAssets() =>
            ShortcutCommandContext.SelectedAssets()
                .Select(AssetDatabase.GetAssetPath)
                .Where(p => !string.IsNullOrEmpty(p) && p.StartsWith("Assets"))
                .Distinct()
                .ToList();
    }
}
