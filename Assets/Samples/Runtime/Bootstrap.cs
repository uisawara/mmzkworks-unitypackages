using Mmzkworks.muSceneManager;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private SceneId _levelScene;

    private enum SceneSlots
    {
        Main,
        Level
    }
    
    private SceneManager<SceneSlots> _sceneManager = new();
    
    private async void Start()
    {
        await _sceneManager.ChangeSubScene(SceneSlots.Level, _levelScene);
    }
}