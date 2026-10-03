using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mmzkworks.muSceneManager.Samples
{
    /// <summary>
    /// Attach to a GameObject in your startup scene and set the scenes from the Inspector.
    /// All scenes must be added to Build Settings.
    /// </summary>
    public class BasicSample : MonoBehaviour
    {
        private enum SceneSlots
        {
            Level,
            Ui
        }

        [SerializeField] private SceneId _mainScene;
        [SerializeField] private SceneId _levelScene;
        [SerializeField] private SceneId _uiScene;

        private readonly SceneManager<SceneSlots> _sceneManager = new();
        private bool _uiVisible;

        private async void Start()
        {
            // Keep this GameObject alive across ChangeMainScene (LoadSceneMode.Single unloads the startup scene)
            DontDestroyOnLoad(gameObject);

            if (_mainScene.IsValid)
            {
                await _sceneManager.ChangeMainScene(_mainScene);
            }

            if (_levelScene.IsValid)
            {
                await _sceneManager.ChangeSubScene(SceneSlots.Level, _levelScene);
            }

            SetUiVisible(true);
        }

        private void OnGUI()
        {
            GUILayout.Label($"Main: {_sceneManager.CurrentMainSceneId} / Status: {_sceneManager.Status}");

            if (_uiScene.IsValid && GUILayout.Button(_uiVisible ? "Hide UI" : "Show UI"))
            {
                SetUiVisible(!_uiVisible);
            }
        }

        private void SetUiVisible(bool visible)
        {
            if (!_uiScene.IsValid)
            {
                return;
            }

            // Requests are queued and processed in order, so toggling repeatedly is safe
            _uiVisible = visible;
            var task = visible
                ? _sceneManager.ChangeSubScene(SceneSlots.Ui, _uiScene)
                : _sceneManager.UnloadSubScene(SceneSlots.Ui);
            task.Forget();
        }
    }
}
