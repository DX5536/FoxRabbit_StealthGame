using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [Esc] pauses: Resume, Options, Restart (from the last checkpoint), Main Menu.
/// Freezes time and player input; disabled on the game over / level complete screen.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject pausePanel;
    [SerializeField] OptionsPanel optionsPanel;
    [Tooltip("Build index of the main menu.")]
    [SerializeField] int menuSceneIndex = 0;

    public static bool IsPaused { get; private set; }

    bool inputBeforePause;

    void Start()
    {
        IsPaused = false;
        pausePanel.SetActive(false);
        optionsPanel.gameObject.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame || KeybindRow.IsListening || GameOver_Manager.IsGameOver) return;

        if (!IsPaused) Pause();
        else if (optionsPanel.gameObject.activeSelf) optionsPanel.Close(); // Esc = back
        else Resume();
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        if (PlayerController.Instance != null)
        {
            inputBeforePause = PlayerController.Instance.InputEnabled; // false during the VN intro: keep it that way
            PlayerController.Instance.InputEnabled = false;
        }
        pausePanel.SetActive(true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (PlayerController.Instance != null) PlayerController.Instance.InputEnabled = inputBeforePause;
        optionsPanel.gameObject.SetActive(false);
        pausePanel.SetActive(false);
    }

    public void OpenOptions()
    {
        pausePanel.SetActive(false);
        optionsPanel.Open();
    }

    /// <summary>Hooked to OptionsPanel.onClosed.</summary>
    public void BackFromOptions()
    {
        if (IsPaused) pausePanel.SetActive(true);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        if (GameOver_Manager.Instance != null) GameOver_Manager.Instance.Restart();
        else SceneLoader.Load(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void MainMenu()
    {
        IsPaused = false;
        SceneLoader.Load(menuSceneIndex);
    }

    void OnDestroy()
    {
        if (IsPaused) Time.timeScale = 1f;
        IsPaused = false;
    }
}
