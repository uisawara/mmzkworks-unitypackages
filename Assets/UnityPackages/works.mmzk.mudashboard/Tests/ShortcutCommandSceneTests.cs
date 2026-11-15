using System.Linq;
using Mmzkworks.muDashboard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muDashboard.Tests
{
    /// <summary>Runs commands end to end in a temporary scene and a temporary asset folder.</summary>
    public sealed class ShortcutCommandSceneTests
    {
        private const string TempFolder = "Assets/__muDashboardCommandTests";

        private Scene _previousScene;
        private Scene _scene;
        private Object[] _previousSelection;

        [SetUp]
        public void SetUp()
        {
            _previousSelection = Selection.objects;
            _previousScene = SceneManager.GetActiveScene();
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(_scene);
        }

        [TearDown]
        public void TearDown()
        {
            if (_previousScene.IsValid()) SceneManager.SetActiveScene(_previousScene);
            if (_scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
            Selection.objects = _previousSelection;
        }

        private static bool Run(string id, out string error, params (string key, string value)[] args)
        {
            var command = ShortcutCommandRegistry.Find(id);
            Assert.That(command, Is.Not.Null, id);
            var item = new DashboardItem { kind = ShortcutItemKind.Command, command = id };
            foreach (var (key, value) in args) item.SetArg(key, value);

            var ctx = new ShortcutCommandContext(command, item);
            var ok = command.Run(ctx);
            error = ctx.Error;
            return ok;
        }

        private static void RunOk(string id, params (string key, string value)[] args)
        {
            Assert.That(Run(id, out var error, args), Is.True, $"{id}: {error}");
        }

        [Test]
        public void CreateRenameAddComponent_ThenPrefabCreateAndInstantiate()
        {
            RunOk("go.createEmpty", ("name", "Root"));
            var root = Selection.activeGameObject;
            Assert.That(root, Is.Not.Null);
            Assert.That(root.scene, Is.EqualTo(_scene));

            RunOk("go.createPrimitive", ("type", "Cube"), ("name", "Box"), ("parent", "Path"), ("parentPath", "Root"));
            var box = Selection.activeGameObject;
            Assert.That(box.transform.parent, Is.EqualTo(root.transform));

            RunOk("go.rename", ("pattern", "{name}_{index}"));
            Assert.That(box.name, Is.EqualTo("Box_1"));

            RunOk("go.addComponent", ("type", "Rigidbody"));
            Assert.That(box.GetComponent<Rigidbody>(), Is.Not.Null);

            RunOk("go.find", ("by", "Path"), ("value", "Root/Box_1"));
            Assert.That(Selection.activeGameObject, Is.EqualTo(box));

            RunOk("prefab.create", ("folder", TempFolder + "/Prefabs"), ("connect", "true"));
            var prefab = Selection.activeObject as GameObject;
            Assert.That(prefab, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(TempFolder + "/Prefabs/Box_1.prefab"));
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(box), Is.True, "connected");

            RunOk("prefab.instantiate", ("parent", "Path"), ("parentPath", "Root"), ("position", "1,2,3"));
            var instance = Selection.activeGameObject;
            Assert.That(instance, Is.Not.EqualTo(box));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance), Is.EqualTo(prefab));
            Assert.That(instance.transform.parent, Is.EqualTo(root.transform));
            Assert.That(instance.transform.localPosition, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(instance.GetComponent<Rigidbody>(), Is.Not.Null);

            RunOk("prefab.unpack", ("mode", "Completely"));
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(instance), Is.False);
        }

        [Test]
        public void Failures_ReportAReason()
        {
            Selection.objects = new Object[0];
            Assert.That(Run("prefab.create", out var e1), Is.False);
            Assert.That(e1, Is.Not.Empty);

            Assert.That(Run("go.find", out var e2, ("by", "Path"), ("value", "Nope/Nothing")), Is.False);
            Assert.That(e2, Does.Contain("Nope/Nothing"));

            RunOk("go.createEmpty", ("name", "A"));
            Assert.That(Run("go.addComponent", out var e3, ("type", "NoSuchComponentXyz")), Is.False);
            Assert.That(e3, Does.Contain("not found"));

            Assert.That(Run("go.createEmpty", out var e4, ("parent", "Path"), ("parentPath", "Missing")), Is.False);
            Assert.That(e4, Does.Contain("Missing"));
        }

        [Test]
        public void SetParent_And_Delete()
        {
            RunOk("go.createEmpty", ("name", "Parent"));
            var parent = Selection.activeGameObject;
            RunOk("go.createEmpty", ("name", "Child"));
            var child = Selection.activeGameObject;

            RunOk("go.setParent", ("path", "Parent"));
            Assert.That(child.transform.parent, Is.EqualTo(parent.transform));

            Selection.activeGameObject = parent;
            Assert.That(Run("go.setParent", out var error, ("path", "Parent/Child")), Is.False, "cannot parent under its own child");
            Assert.That(error, Is.Not.Empty);

            RunOk("go.delete");
            Assert.That(parent == null, Is.True);
            Assert.That(Selection.objects, Is.Empty);
        }

        [Test]
        public void CreateFolder_DuplicateAndMoveAsset()
        {
            RunOk("asset.createFolder", ("path", TempFolder + "/A"));
            Assert.That(AssetDatabase.IsValidFolder(TempFolder + "/A"), Is.True);

            RunOk("go.createEmpty", ("name", "Thing"));
            RunOk("prefab.create", ("folder", TempFolder + "/A"), ("connect", "false"));
            RunOk("asset.duplicate");
            var copyPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            Assert.That(copyPath, Does.StartWith(TempFolder + "/A/Thing").And.Not.EqualTo(TempFolder + "/A/Thing.prefab"));

            RunOk("asset.move", ("folder", TempFolder + "/B"));
            var moved = Selection.objects.Select(AssetDatabase.GetAssetPath).Single();
            Assert.That(moved, Does.StartWith(TempFolder + "/B/"));
        }
    }
}
