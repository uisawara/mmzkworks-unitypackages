using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muSceneManager
{
    /// <summary>
    /// ISceneLoader implementation using UnityEngine.SceneManagement.SceneManager.
    /// </summary>
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public async UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode)
        {
            var operation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, mode);
            if (operation == null)
            {
                throw new InvalidOperationException($"Failed to load scene: {sceneName} (not in build settings?)");
            }

            await operation.ToUniTask();
        }

        public async UniTask UnloadSceneAsync(string sceneName)
        {
            var operation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);
            if (operation == null)
            {
                throw new InvalidOperationException($"Failed to unload scene: {sceneName} (not loaded, or the last loaded scene?)");
            }

            await operation.ToUniTask();
        }
    }
}
