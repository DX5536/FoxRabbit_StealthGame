using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Music / SFX volume, keybinding and Back. Used by the Main Menu and the Pause menu.
/// Open() / Close() just toggle the GameObject and raise events - add your own animation on those events
/// (or on the panel's OnEnable).
/// </summary>
public class OptionsPanel : MonoBehaviour
{
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider sfxSlider;
    [Tooltip("Rows are created here, one per GameAction, from the template.")]
    [SerializeField] Transform keybindContainer;
    [SerializeField] KeybindRow rowTemplate;
    [SerializeField] Button resetKeysButton;
    [SerializeField] Button backButton;

    public UnityEvent onOpened;
    public UnityEvent onClosed;

    bool built;

    void Awake()
    {
        musicSlider.onValueChanged.AddListener(v => SoundManager.Instance?.SetMusicVolume(v));
        sfxSlider.onValueChanged.AddListener(v => SoundManager.Instance?.SetSfxVolume(v));
        resetKeysButton.onClick.AddListener(Keybinds.ResetAll);
        backButton.onClick.AddListener(Close);
        BuildRows();
    }

    void OnEnable()
    {
        if (SoundManager.Instance == null) return;
        musicSlider.SetValueWithoutNotify(SoundManager.Instance.MusicVolume01);
        sfxSlider.SetValueWithoutNotify(SoundManager.Instance.SfxVolume01);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        onOpened?.Invoke();
    }

    public void Close()
    {
        if (KeybindRow.IsListening) return; // Esc / Back while rebinding just cancels the rebinding
        gameObject.SetActive(false);
        onClosed?.Invoke();
    }

    void BuildRows()
    {
        if (built || rowTemplate == null) return;
        built = true;
        rowTemplate.gameObject.SetActive(false);
        foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
        {
            var row = Instantiate(rowTemplate, keybindContainer);
            row.gameObject.SetActive(true);
            row.Init(action);
        }
    }
}
