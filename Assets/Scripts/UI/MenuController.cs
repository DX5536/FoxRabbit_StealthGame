using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Main menu: Start, Options, Credits all live in one scene and stay active.
/// This script never shows/hides panels - the buttons only raise events, so your own tweens decide what moves where.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Tooltip("Build index of the first game scene (File > Build Profiles).")]
    [SerializeField] int startSceneIndex = 1;

    [Header("Hook your tweens here")]
    public UnityEvent onOpenOptions = new UnityEvent();
    public UnityEvent onOpenCredits = new UnityEvent();
    [Tooltip("Back from Options or Credits.")]
    public UnityEvent onBackToMain = new UnityEvent();

    void Start() => Time.timeScale = 1f;

    public void StartGame()
    {
        Checkpoint.Clear();                // a new run starts at the beginning...
        VisualNovel_Manager.ResetPlayed(); // ...and sees the intro again
        SceneLoader.Load(startSceneIndex);
    }

    public void OpenOptions() => onOpenOptions?.Invoke();
    public void OpenCredits() => onOpenCredits?.Invoke();
    public void ShowMain() => onBackToMain?.Invoke();
}
