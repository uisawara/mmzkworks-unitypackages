using Mmzkworks.muDashboard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muDashboard.Tests
{
    /// <summary>Scene object shortcuts, in a temporary scene saved under a temporary folder.</summary>
    public sealed class DashboardSceneObjectsTests
    {
        private const string TempFolder = "Assets/__muDashboardSceneObjectTests";
        private const string ScenePath = TempFolder + "/SceneObjects.unity";

        private Scene _previousScene;
        private Scene _scene;
        private bool _additive;
        private Object[] _previousSelection;

        [SetUp]
        public void SetUp()
        {
            _previousSelection = Selection.objects;
            _previousScene = SceneManager.GetActiveScene();

            // See ShortcutCommandSceneTests: no additive scene next to an unsaved untitled one.
            _additive = !HasUntitledScene();
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                _additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(_scene);
        }

        private static bool HasUntitledScene()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) return true;
            }
            return false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_additive)
            {
                if (_previousScene.IsValid()) SceneManager.SetActiveScene(_previousScene);
                if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
            Selection.objects = _previousSelection;
        }

        /// <summary>Creates Env/Oak in the test scene and saves the scene.</summary>
        private GameObject CreateSavedOak()
        {
            var env = new GameObject("Env");
            var oak = new GameObject("Oak");
            oak.transform.SetParent(env.transform);

            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "__muDashboardSceneObjectTests");
            Assert.That(EditorSceneManager.SaveScene(_scene, ScenePath), Is.True);
            return oak;
        }

        private static DashboardItem Register(GameObject go)
        {
            var item = new DashboardItem { id = DashboardItem.NewId() };
            DashboardSceneObjects.Assign(item, go);
            return item;
        }

        [Test]
        public void ToSceneGameObject_AcceptsSceneGameObjectsAndTheirComponents()
        {
            var go = new GameObject("A");
            Assert.That(DashboardSceneObjects.ToSceneGameObject(go), Is.SameAs(go));
            Assert.That(DashboardSceneObjects.ToSceneGameObject(go.transform), Is.SameAs(go));
            Assert.That(DashboardSceneObjects.ToSceneGameObject(null), Is.Null);
        }

        [Test]
        public void ToSceneGameObject_RejectsAssets()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>("Packages/works.mmzk.mudashboard/package.json")
                        ?? AssetDatabase.LoadAssetAtPath<Object>("Assets/UnityPackages/works.mmzk.mudashboard/package.json");
            Assume.That(asset, Is.Not.Null);
            Assert.That(DashboardSceneObjects.ToSceneGameObject(asset), Is.Null);
        }

        [Test]
        public void CanRegister_FailsForObjectsInAnUnsavedScene()
        {
            var go = new GameObject("Unsaved");
            Assert.That(DashboardSceneObjects.CanRegister(go, out var error), Is.False);
            Assert.That(error, Does.Contain("Save the scene"));
        }

        [Test]
        public void Assign_StoresIdScenePathAndHierarchyPath()
        {
            var oak = CreateSavedOak();
            Assert.That(DashboardSceneObjects.CanRegister(oak, out _), Is.True);

            var item = Register(oak);

            Assert.That(item.kind, Is.EqualTo(ShortcutItemKind.SceneObject));
            Assert.That(item.globalId, Is.Not.Empty);
            Assert.That(item.path, Is.EqualTo(ScenePath));
            Assert.That(item.guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(ScenePath)));
            Assert.That(item.hierarchy, Is.EqualTo("Env/Oak"));
        }

        [Test]
        public void Resolve_FindsTheObjectAndDescribesIt()
        {
            var oak = CreateSavedOak();
            var item = Register(oak);

            var resolved = DashboardSceneObjects.Resolve(item);
            Assert.That(resolved, Is.SameAs(oak));
            Assert.That(DashboardSceneObjects.ObjectName(item, resolved), Is.EqualTo("Oak"));
            Assert.That(DashboardSceneObjects.Location(item, resolved), Is.EqualTo("SceneObjects: Env"));
            Assert.That(DashboardSceneObjects.FullPath(item, resolved), Is.EqualTo(ScenePath + ": Env/Oak"));
        }

        [Test]
        public void Resolve_DoesNotFallBackToTheHierarchyPath()
        {
            var item = Register(CreateSavedOak());
            item.globalId = "";

            Assert.That(DashboardSceneObjects.Resolve(item), Is.Null);
        }

        [Test]
        public void ScenePath_UsesOnlyTheGuid()
        {
            var item = Register(CreateSavedOak());
            item.guid = "00000000000000000000000000000000";

            // The stored path still points to the open scene, but the link is by GUID only.
            Assert.That(DashboardSceneObjects.ScenePath(item), Is.Null);
            Assert.That(DashboardSceneObjects.IsSceneOpen(item), Is.False);
            Assert.That(DashboardSceneObjects.TrySelect(item, out var error), Is.False);
            Assert.That(error, Does.Contain("Missing scene"));
        }

        [Test]
        public void TrySelect_SelectsTheObject()
        {
            var oak = CreateSavedOak();
            var item = Register(oak);

            Assert.That(DashboardSceneObjects.TrySelect(item, out var error), Is.True, error);
            Assert.That(Selection.activeGameObject, Is.SameAs(oak));
        }

        [Test]
        public void ClosedScene_IsNotOpenAndCannotBeSelected()
        {
            var item = Register(CreateSavedOak());

            if (_additive)
            {
                if (_previousScene.IsValid()) SceneManager.SetActiveScene(_previousScene);
                EditorSceneManager.CloseScene(_scene, true);
            }
            else
            {
                // The only scene: replacing it unloads the saved one.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            Assert.That(DashboardSceneObjects.IsSceneOpen(item), Is.False);
            Assert.That(DashboardSceneObjects.Resolve(item), Is.Null);
            Assert.That(DashboardSceneObjects.ObjectName(item, null), Is.EqualTo("Oak"));
            Assert.That(DashboardSceneObjects.TrySelect(item, out var error), Is.False);
            Assert.That(error, Does.Contain("not open"));
        }

        [Test]
        public void SceneObjectFields_SurviveSerialization()
        {
            var doc = new DashboardDocument();
            doc.items.Add(new DashboardItem
            {
                id = "a",
                kind = ShortcutItemKind.SceneObject,
                globalId = "GlobalObjectId_V1-2-abc-123-0",
                hierarchy = "Env/Oak",
                path = "Assets/Main.unity"
            });

            var loaded = DashboardSerializer.FromJson(DashboardSerializer.ToJson(doc));

            var item = loaded.Find("a");
            Assert.That(item.kind, Is.EqualTo(ShortcutItemKind.SceneObject));
            Assert.That(item.globalId, Is.EqualTo("GlobalObjectId_V1-2-abc-123-0"));
            Assert.That(item.hierarchy, Is.EqualTo("Env/Oak"));
            Assert.That(ShortcutItemKind.IsRunnable(item.kind), Is.True);
        }

        [Test]
        public void OldPanels_WithoutSceneObjectFields_LoadWithEmptyStrings()
        {
            var doc = DashboardSerializer.FromJson("{\"version\":1,\"items\":[{\"id\":\"a\",\"kind\":\"asset\",\"path\":\"Assets/x.txt\"}]}");
            Assert.That(doc.items[0].globalId, Is.EqualTo(""));
            Assert.That(doc.items[0].hierarchy, Is.EqualTo(""));
        }
    }
}
