using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mmzkworks.muLogger;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muSceneManager
{
    public enum SceneLoadStatus
    {
        Idle,
        Loading,
        Unloading
    }

    public class SceneManager<TSceneSlot>
        where TSceneSlot : Enum
    {
        private SceneId _mainSceneId;
        private readonly Dictionary<TSceneSlot, SceneId> _sceneMap = new();
        private readonly ILogger _logger;
        private readonly ISceneLoader _sceneLoader;

        // For queuing operations
        private readonly Queue<Func<UniTask>> _operationQueue = new();
        private bool _isProcessingQueue = false;

        // Status
        public SceneLoadStatus Status { get; private set; } = SceneLoadStatus.Idle;
        public SceneId CurrentMainSceneId => _mainSceneId;
        public bool HasMainScene => _mainSceneId.IsValid;

        public SceneManager(ILogger logger = null, ISceneLoader sceneLoader = null)
        {
            _logger = logger ?? new UnityLogger(nameof(SceneManager<TSceneSlot>));
            _sceneLoader = sceneLoader ?? new UnitySceneLoader();
        }

        private async UniTask EnqueueOperation(Func<UniTask> operation)
        {
            var tcs = new UniTaskCompletionSource();

            lock (_operationQueue)
            {
                _operationQueue.Enqueue(async () =>
                {
                    try
                    {
                        await operation();
                        tcs.TrySetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

                if (!_isProcessingQueue)
                {
                    _isProcessingQueue = true;
                    ProcessQueue().Forget();
                }
            }

            await tcs.Task;
        }

        private async UniTaskVoid ProcessQueue()
        {
            while (true)
            {
                Func<UniTask> operation = null;

                lock (_operationQueue)
                {
                    if (_operationQueue.Count == 0)
                    {
                        _isProcessingQueue = false;
                        return;
                    }

                    operation = _operationQueue.Dequeue();
                }

                await operation();
            }
        }

        public async UniTask ChangeMainScene(SceneId sceneId)
        {
            _logger.Log($"ChangeMainScene called: {sceneId}");
            ThrowIfInvalid(sceneId, nameof(sceneId));

            await EnqueueOperation(async () =>
            {
                // Ignore if current scene is already the target scene (checked inside the queue to see the latest state)
                if (_mainSceneId == sceneId)
                {
                    _logger.Log($"ChangeMainScene ignored: Already on scene {sceneId}");
                    return;
                }

                try
                {
                    // LoadSceneMode.Single unloads all loaded scenes (previous main scene and sub scenes)
                    _logger.Log($"Loading new main scene: {sceneId}");
                    Status = SceneLoadStatus.Loading;
                    await _sceneLoader.LoadSceneAsync(sceneId.Name, LoadSceneMode.Single);
                    _mainSceneId = sceneId;
                    _sceneMap.Clear();
                    _logger.Log($"Main scene loaded: {sceneId}");
                }
                finally
                {
                    Status = SceneLoadStatus.Idle;
                }
            });
        }

        public async UniTask ChangeSubScene(TSceneSlot slot, SceneId id)
        {
            _logger.Log($"ChangeSubScene called: Slot={slot}, SceneId={id}");
            ThrowIfInvalid(id, nameof(id));

            await EnqueueOperation(async () =>
            {
                // Ignore if current scene in slot is already the target scene (checked inside the queue to see the latest state)
                if (_sceneMap.TryGetValue(slot, out var currentId) && currentId == id)
                {
                    _logger.Log($"ChangeSubScene ignored: Already on scene {id} in slot {slot}");
                    return;
                }

                try
                {
                    // Unload existing scene in the same slot if any
                    if (_sceneMap.TryGetValue(slot, out var previousSceneId))
                    {
                        _logger.Log($"Unloading previous sub scene: Slot={slot}, SceneId={previousSceneId}");
                        Status = SceneLoadStatus.Unloading;
                        await _sceneLoader.UnloadSceneAsync(previousSceneId.Name);
                        _sceneMap.Remove(slot);
                        _logger.Log($"Previous sub scene unloaded: Slot={slot}, SceneId={previousSceneId}");
                    }

                    // Load new scene
                    _logger.Log($"Loading new sub scene: Slot={slot}, SceneId={id}");
                    Status = SceneLoadStatus.Loading;
                    await _sceneLoader.LoadSceneAsync(id.Name, LoadSceneMode.Additive);
                    _sceneMap[slot] = id;
                    _logger.Log($"Sub scene loaded: Slot={slot}, SceneId={id}");
                }
                finally
                {
                    Status = SceneLoadStatus.Idle;
                }
            });
        }

        public async UniTask UnloadSubScene(TSceneSlot slot)
        {
            _logger.Log($"UnloadSubScene called: Slot={slot}");

            await EnqueueOperation(async () =>
            {
                if (!_sceneMap.TryGetValue(slot, out var id))
                {
                    _logger.Log($"UnloadSubScene ignored: No scene in slot {slot}");
                    return;
                }

                Status = SceneLoadStatus.Unloading;
                try
                {
                    _logger.Log($"Unloading sub scene: Slot={slot}, SceneId={id}");
                    await _sceneLoader.UnloadSceneAsync(id.Name);
                    _sceneMap.Remove(slot);
                    _logger.Log($"Sub scene unloaded: Slot={slot}, SceneId={id}");
                }
                finally
                {
                    Status = SceneLoadStatus.Idle;
                }
            });
        }

        private static void ThrowIfInvalid(SceneId id, string paramName)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("SceneId is invalid (default).", paramName);
            }
        }
    }
}
