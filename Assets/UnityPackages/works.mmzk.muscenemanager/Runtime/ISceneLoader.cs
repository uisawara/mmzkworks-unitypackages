using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Mmzkworks.muSceneManager
{
    /// <summary>
    /// Abstraction of scene loading/unloading so that SceneManager can be tested without real scenes.
    /// </summary>
    public interface ISceneLoader
    {
        UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode);
        UniTask UnloadSceneAsync(string sceneName);
    }
}
