using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muSceneManager.Tests
{
    /// <summary>
    /// ISceneLoader mock that records calls.
    /// Completes immediately by default; with ManualComplete, each call stays pending until CompleteNext() is called.
    /// </summary>
    internal sealed class MockSceneLoader : ISceneLoader
    {
        public readonly List<string> Calls = new();
        public readonly HashSet<string> FailingLoads = new();
        public readonly HashSet<string> FailingUnloads = new();
        public bool ManualComplete { get; set; }

        private readonly Queue<UniTaskCompletionSource> _pending = new();
        public int PendingCount => _pending.Count;

        public UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode)
        {
            Calls.Add($"Load:{sceneName}:{mode}");
            return Run(FailingLoads.Contains(sceneName), $"load {sceneName}");
        }

        public UniTask UnloadSceneAsync(string sceneName)
        {
            Calls.Add($"Unload:{sceneName}");
            return Run(FailingUnloads.Contains(sceneName), $"unload {sceneName}");
        }

        public void CompleteNext()
        {
            _pending.Dequeue().TrySetResult();
        }

        private UniTask Run(bool fail, string description)
        {
            if (fail)
            {
                return UniTask.FromException(new InvalidOperationException($"Mock failed to {description}"));
            }

            if (!ManualComplete)
            {
                return UniTask.CompletedTask;
            }

            var tcs = new UniTaskCompletionSource();
            _pending.Enqueue(tcs);
            return tcs.Task;
        }
    }
}
