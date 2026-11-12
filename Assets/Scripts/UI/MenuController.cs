using UnityEngine;

/// <summary>Main menu: Start, Options, Credits. Panels are plain GameObjects - animate them however you like.</summary>
public class MenuController : MonoBehaviour
{
    [Tooltip("Build index of the first game scene (File > Build Profiles).")]
    [SerializeField] int startSceneIndex = 1;
    [SerializeField] GameObject mainPanel;
    [SerializeField] OptionsPanel optionsPanel;
    [SerializeField] GameObject creditsPanel;

    void Start()
    {
        Time.timeScale = 1f;
        ShowMain();
    }

    public void StartGame()
    {
        Checkpoint.Clear();                // a new run starts at the beginning...
        VisualNovel_Manager.ResetPlayed(); // ...and sees the intro again
        SceneLoader.Load(startSceneIndex);
    }

    public void OpenOptions()
    {
        mainPanel.SetActive(false);
        optionsPanel.Open();
    }

    public void OpenCredits()
    {
        mainPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    /// <summary>Back buttons (Options' onClosed event and the Credits back button) land here.</summary>
    public void ShowMain()
    {
        if (optionsPanel.gameObject.activeSelf) optionsPanel.Close();
        creditsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }
}
