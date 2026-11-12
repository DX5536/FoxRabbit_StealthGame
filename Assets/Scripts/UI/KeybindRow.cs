using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>One "Action [Key]" row in the Options panel. Click the key, press a new one (Esc cancels).</summary>
public class KeybindRow : MonoBehaviour
{
    [SerializeField] TMP_Text actionLabel;
    [SerializeField] TMP_Text keyLabel;
    [SerializeField] Button button;

    /// <summary>True while any row waits for a key (the pause menu ignores Esc then).</summary>
    public static bool IsListening => listening != null || consumedFrame == Time.frameCount; // also the frame the key was eaten
    static KeybindRow listening;
    static int consumedFrame = -1;

    GameAction action;
    bool initialised;

    public void Init(GameAction gameAction)
    {
        action = gameAction;
        initialised = true;
        actionLabel.text = Nicify(gameAction.ToString());
        button.onClick.AddListener(StartListening);
        Refresh();
    }

    void OnEnable() => Keybinds.Changed += Refresh;

    void OnDisable()
    {
        Keybinds.Changed -= Refresh;
        if (listening == this) listening = null;
    }

    void StartListening()
    {
        if (listening != null) listening.Refresh();
        listening = this;
        keyLabel.text = "Press a key...";
    }

    void Update()
    {
        if (listening != this || Keyboard.current == null) return;
        foreach (KeyControl key in Keyboard.current.allKeys)
        {
            if (key == null || !key.wasPressedThisFrame) continue;
            listening = null;
            consumedFrame = Time.frameCount;
            if (key.keyCode != Key.Escape) Keybinds.Set(action, key.keyCode);
            Refresh();
            return;
        }
    }

    void Refresh()
    {
        if (!initialised) return;
        keyLabel.text = Keybinds.Label(action);
    }

    static string Nicify(string s) => System.Text.RegularExpressions.Regex.Replace(s, "(?<!^)([A-Z])", " $1");
}
