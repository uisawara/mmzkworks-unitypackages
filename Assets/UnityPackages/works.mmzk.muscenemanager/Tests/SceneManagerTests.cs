using System;
using Cysharp.Threading.Tasks;
using Mmzkworks.muLogger;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muSceneManager.Tests
{
    public enum TestSceneSlot
    {
        Slot1,
        Slot2,
        Slot3
    }

    public static class TestSceneId
    {
        public static readonly SceneId Scene1 = new("Scene1");
        public static readonly SceneId Scene2 = new("Scene2");
        public static readonly SceneId Scene3 = new("Scene3");
    }

    public class SceneManagerTests
    {
        private MockSceneLoader _loader;
        private SceneManager<TestSceneSlot> _sceneManager;

        [SetUp]
        public void SetUp()
        {
            _loader = new MockSceneLoader();
            _sceneManager = new SceneManager<TestSceneSlot>(new NullLogger(), _loader);
        }

        [TearDown]
        public void TearDown()
        {
            _sceneManager = null;
            _loader = null;
        }

        private static void AssertSucceeded(UniTask task)
        {
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            task.GetAwaiter().GetResult();
        }

        private static void AssertFaulted(UniTask task) => AssertFaulted<InvalidOperationException>(task);

        private static void AssertFaulted<TException>(UniTask task) where TException : Exception
        {
            Assert.AreEqual(UniTaskStatus.Faulted, task.Status);
            Assert.Throws<TException>(() => task.GetAwaiter().GetResult());
        }

        private static string Load(SceneId id, LoadSceneMode mode) => $"Load:{id.Name}:{mode}";
        private static string Unload(SceneId id) => $"Unload:{id.Name}";

        // ---- Initial state ----

        [Test]
        public void Initial_State_Is_Idle_And_Has_No_Main_Scene()
        {
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);
            Assert.IsFalse(_sceneManager.HasMainScene);
            Assert.AreEqual(default(SceneId), _sceneManager.CurrentMainSceneId);
        }

        // ---- ChangeMainScene ----

        [Test]
        public void ChangeMainScene_Loads_Scene_In_Single_Mode()
        {
            AssertSucceeded(_sceneManager.ChangeMainScene(TestSceneId.Scene2));

            CollectionAssert.AreEqual(new[] { Load(TestSceneId.Scene2, LoadSceneMode.Single) }, _loader.Calls);
            Assert.IsTrue(_sceneManager.HasMainScene);
            Assert.AreEqual(TestSceneId.Scene2, _sceneManager.CurrentMainSceneId);
        }

        [Test]
        public void ChangeMainScene_With_Invalid_SceneId_Fails()
        {
            AssertFaulted<ArgumentException>(_sceneManager.ChangeMainScene(default));

            CollectionAssert.IsEmpty(_loader.Calls);
            Assert.IsFalse(_sceneManager.HasMainScene);
        }

        [Test]
        public void ChangeMainScene_Does_Not_Unload_Previous_Scene_Explicitly()
        {
            AssertSucceeded(_sceneManager.ChangeMainScene(TestSceneId.Scene1));
            AssertSucceeded(_sceneManager.ChangeMainScene(TestSceneId.Scene2));

            // LoadSceneMode.Single replaces the previous scene, so no explicit unload is issued
            CollectionAssert.AreEqual(new[]
            {
                Load(TestSceneId.Scene1, LoadSceneMode.Single),
                Load(TestSceneId.Scene2, LoadSceneMode.Single)
            }, _loader.Calls);
            Assert.AreEqual(TestSceneId.Scene2, _sceneManager.CurrentMainSceneId);
        }

        [Test]
        public void ChangeMainScene_To_Same_Scene_Is_Ignored()
        {
            AssertSucceeded(_sceneManager.ChangeMainScene(TestSceneId.Scene1));
            AssertSucceeded(_sceneManager.ChangeMainScene(TestSceneId.Scene1));

            CollectionAssert.AreEqual(new[] { Load(TestSceneId.Scene1, LoadSceneMode.Single) }, _loader.Calls);
        }

        [Test]
        public void ChangeMainScene_Status_Is_Loading_While_Loading()
        {
            _loader.ManualComplete = true;

            var task = _sceneManager.ChangeMainScene(TestSceneId.Scene1);
            Assert.AreEqual(SceneLoadStatus.Loading, _sceneManager.Status);
            Assert.AreEqual(UniTaskStatus.Pending, task.Status);

            _loader.CompleteNext();
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);
            AssertSucceeded(task);
        }

        [Test]
        public void ChangeMainScene_Clears_Sub_Scenes()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene2));
            AssertSucceeded(_sceneManager.ChangeMainScene(TestSceneId.Scene1));
            _loader.Calls.Clear();

            // Sub scenes were unloaded by Single mode, so there is nothing to unload
            AssertSucceeded(_sceneManager.UnloadSubScene(TestSceneSlot.Slot1));
            CollectionAssert.IsEmpty(_loader.Calls);

            // Loading the same sub scene again is not ignored and does not unload anything
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene2));
            CollectionAssert.AreEqual(new[] { Load(TestSceneId.Scene2, LoadSceneMode.Additive) }, _loader.Calls);
        }

        [Test]
        public void ChangeMainScene_Failure_Keeps_State()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene2));
            _loader.FailingLoads.Add(TestSceneId.Scene3.Name);

            AssertFaulted(_sceneManager.ChangeMainScene(TestSceneId.Scene3));
            Assert.IsFalse(_sceneManager.HasMainScene);
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);

            // Sub scene is still tracked
            _loader.Calls.Clear();
            AssertSucceeded(_sceneManager.UnloadSubScene(TestSceneSlot.Slot1));
            CollectionAssert.AreEqual(new[] { Unload(TestSceneId.Scene2) }, _loader.Calls);
        }

        // ---- ChangeSubScene ----

        [Test]
        public void ChangeSubScene_Loads_Scene_In_Additive_Mode()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));

            CollectionAssert.AreEqual(new[] { Load(TestSceneId.Scene1, LoadSceneMode.Additive) }, _loader.Calls);
        }

        [Test]
        public void ChangeSubScene_With_Invalid_SceneId_Fails()
        {
            AssertFaulted<ArgumentException>(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, default));

            CollectionAssert.IsEmpty(_loader.Calls);
        }

        [Test]
        public void ChangeSubScene_To_Same_Scene_Is_Ignored()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));

            CollectionAssert.AreEqual(new[] { Load(TestSceneId.Scene1, LoadSceneMode.Additive) }, _loader.Calls);
        }

        [Test]
        public void ChangeSubScene_Replaces_Scene_In_Slot()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            _loader.ManualComplete = true;

            var task = _sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene2);
            Assert.AreEqual(SceneLoadStatus.Unloading, _sceneManager.Status);
            Assert.AreEqual(Unload(TestSceneId.Scene1), _loader.Calls[^1]);

            _loader.CompleteNext();
            Assert.AreEqual(SceneLoadStatus.Loading, _sceneManager.Status);
            Assert.AreEqual(Load(TestSceneId.Scene2, LoadSceneMode.Additive), _loader.Calls[^1]);

            _loader.CompleteNext();
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);
            AssertSucceeded(task);
        }

        [Test]
        public void ChangeSubScene_Slots_Are_Independent()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot2, TestSceneId.Scene2));

            CollectionAssert.AreEqual(new[]
            {
                Load(TestSceneId.Scene1, LoadSceneMode.Additive),
                Load(TestSceneId.Scene2, LoadSceneMode.Additive)
            }, _loader.Calls);
        }

        [Test]
        public void ChangeSubScene_Load_Failure_Leaves_Slot_Empty()
        {
            _loader.FailingLoads.Add(TestSceneId.Scene1.Name);
            AssertFaulted(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);

            // Retrying is not ignored and does not try to unload the failed scene
            _loader.FailingLoads.Clear();
            _loader.Calls.Clear();
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            CollectionAssert.AreEqual(new[] { Load(TestSceneId.Scene1, LoadSceneMode.Additive) }, _loader.Calls);
        }

        [Test]
        public void ChangeSubScene_Load_Failure_After_Unload_Leaves_Slot_Empty()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            _loader.FailingLoads.Add(TestSceneId.Scene2.Name);

            AssertFaulted(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene2));

            // Previous scene was already unloaded, so loading it again is not ignored
            _loader.Calls.Clear();
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            CollectionAssert.AreEqual(new[] { Load(TestSceneId.Scene1, LoadSceneMode.Additive) }, _loader.Calls);
        }

        // ---- UnloadSubScene ----

        [Test]
        public void UnloadSubScene_Unloads_Scene_In_Slot()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            _loader.ManualComplete = true;

            var task = _sceneManager.UnloadSubScene(TestSceneSlot.Slot1);
            Assert.AreEqual(SceneLoadStatus.Unloading, _sceneManager.Status);
            Assert.AreEqual(Unload(TestSceneId.Scene1), _loader.Calls[^1]);

            _loader.CompleteNext();
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);
            AssertSucceeded(task);

            // Slot is now empty
            _loader.Calls.Clear();
            AssertSucceeded(_sceneManager.UnloadSubScene(TestSceneSlot.Slot1));
            CollectionAssert.IsEmpty(_loader.Calls);
        }

        [Test]
        public void UnloadSubScene_Empty_Slot_Is_Ignored()
        {
            AssertSucceeded(_sceneManager.UnloadSubScene(TestSceneSlot.Slot1));

            CollectionAssert.IsEmpty(_loader.Calls);
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);
        }

        [Test]
        public void UnloadSubScene_Failure_Keeps_Slot()
        {
            AssertSucceeded(_sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene1));
            _loader.FailingUnloads.Add(TestSceneId.Scene1.Name);

            AssertFaulted(_sceneManager.UnloadSubScene(TestSceneSlot.Slot1));
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);

            // Slot is still tracked, so it can be retried
            _loader.FailingUnloads.Clear();
            _loader.Calls.Clear();
            AssertSucceeded(_sceneManager.UnloadSubScene(TestSceneSlot.Slot1));
            CollectionAssert.AreEqual(new[] { Unload(TestSceneId.Scene1) }, _loader.Calls);
        }

        // ---- Queue ----

        [Test]
        public void Requests_Are_Processed_Sequentially_In_Order()
        {
            _loader.ManualComplete = true;

            var task1 = _sceneManager.ChangeMainScene(TestSceneId.Scene1);
            var task2 = _sceneManager.ChangeSubScene(TestSceneSlot.Slot1, TestSceneId.Scene2);
            var task3 = _sceneManager.UnloadSubScene(TestSceneSlot.Slot1);

            // Only the first request has started
            CollectionAssert.AreEqual(new[] { Load(TestSceneId.Scene1, LoadSceneMode.Single) }, _loader.Calls);
            Assert.AreEqual(1, _loader.PendingCount);

            _loader.CompleteNext();
            Assert.AreEqual(UniTaskStatus.Succeeded, task1.Status);
            Assert.AreEqual(UniTaskStatus.Pending, task2.Status);
            Assert.AreEqual(Load(TestSceneId.Scene2, LoadSceneMode.Additive), _loader.Calls[^1]);

            _loader.CompleteNext();
            Assert.AreEqual(UniTaskStatus.Succeeded, task2.Status);
            Assert.AreEqual(UniTaskStatus.Pending, task3.Status);
            Assert.AreEqual(Unload(TestSceneId.Scene2), _loader.Calls[^1]);

            _loader.CompleteNext();
            AssertSucceeded(task1);
            AssertSucceeded(task2);
            AssertSucceeded(task3);
            Assert.AreEqual(3, _loader.Calls.Count);
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);
        }

        [Test]
        public void Duplicate_Check_Uses_State_After_Pending_Requests()
        {
            AssertSucceeded(_sceneManager.ChangeMainScene(TestSceneId.Scene1));
            _loader.ManualComplete = true;

            // Scene1 -> Scene2 -> Scene1: the last request must not be ignored even though Scene1 is current at call time
            var task1 = _sceneManager.ChangeMainScene(TestSceneId.Scene2);
            var task2 = _sceneManager.ChangeMainScene(TestSceneId.Scene1);

            _loader.CompleteNext();
            _loader.CompleteNext();
            AssertSucceeded(task1);
            AssertSucceeded(task2);

            CollectionAssert.AreEqual(new[]
            {
                Load(TestSceneId.Scene1, LoadSceneMode.Single),
                Load(TestSceneId.Scene2, LoadSceneMode.Single),
                Load(TestSceneId.Scene1, LoadSceneMode.Single)
            }, _loader.Calls);
            Assert.AreEqual(TestSceneId.Scene1, _sceneManager.CurrentMainSceneId);
        }

        [Test]
        public void Failed_Request_Does_Not_Block_Following_Requests()
        {
            _loader.FailingLoads.Add(TestSceneId.Scene1.Name);

            var task1 = _sceneManager.ChangeMainScene(TestSceneId.Scene1);
            var task2 = _sceneManager.ChangeMainScene(TestSceneId.Scene2);

            AssertFaulted(task1);
            AssertSucceeded(task2);
            Assert.AreEqual(TestSceneId.Scene2, _sceneManager.CurrentMainSceneId);
            Assert.AreEqual(SceneLoadStatus.Idle, _sceneManager.Status);
        }
    }
}
