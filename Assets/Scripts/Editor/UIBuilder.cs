using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Small helpers to build placeholder uGUI from editor scripts (Menu, Options, Pause, VN stage).</summary>
public static partial class TestSceneBuilder
{
    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.13f, 0.94f);

    internal static Canvas UICanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    internal static RectTransform UIRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    internal static Image UIPanel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var img = UIRect(name, parent, pos, size).gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    internal static TextMeshProUGUI UIText(string name, Transform parent, string text, Vector2 pos, Vector2 size, float fontSize,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var t = UIRect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        return t;
    }

    internal static Button UIButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, UnityAction onClick = null)
    {
        var go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var text = go.GetComponentInChildren<TMP_Text>();
        text.text = label;
        text.fontSize = 32;
        var button = go.GetComponent<Button>();
        if (onClick != null) UnityEventTools.AddPersistentListener(button.onClick, onClick);
        return button;
    }

    internal static Slider UISlider(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var slider = go.GetComponent<Slider>();
        slider.value = 1f;
        foreach (var img in go.GetComponentsInChildren<Image>())
            img.color = img.name == "Fill" ? new Color(0.4f, 0.8f, 1f) : img.name == "Handle" ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        return slider;
    }

    /// <summary>Options panel (volume sliders, keybind rows, reset, back). Starts hidden.</summary>
    internal static OptionsPanel BuildOptionsPanel(Transform parent)
    {
        var panel = UIPanel("OptionsPanel", parent, Vector2.zero, new Vector2(1000, 900), PanelColor);
        UIText("Title", panel.transform, "OPTIONS", new Vector2(0, 390), new Vector2(800, 80), 56);

        UIText("MusicLabel", panel.transform, "Music", new Vector2(-280, 290), new Vector2(300, 50), 34, TextAlignmentOptions.Left);
        var music = UISlider("MusicSlider", panel.transform, new Vector2(120, 290), new Vector2(500, 30));
        UIText("SfxLabel", panel.transform, "Sound FX", new Vector2(-280, 220), new Vector2(300, 50), 34, TextAlignmentOptions.Left);
        var sfx = UISlider("SfxSlider", panel.transform, new Vector2(120, 220), new Vector2(500, 30));

        UIText("KeysTitle", panel.transform, "Keybinding  <size=60%>(click a key, then press the new one - Esc cancels)</size>",
            new Vector2(0, 140), new Vector2(900, 50), 32);
        var list = UIRect("KeybindList", panel.transform, new Vector2(0, -90), new Vector2(800, 400));
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        var rowRt = UIRect("KeybindRowTemplate", list, Vector2.zero, new Vector2(800, 46));
        rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
        var actionLabel = UIText("Action", rowRt, "Action", new Vector2(-170, 0), new Vector2(440, 46), 30, TextAlignmentOptions.Left);
        var keyButton = UIButton("KeyButton", rowRt, "Key", new Vector2(230, 0), new Vector2(300, 42));
        var row = rowRt.gameObject.AddComponent<KeybindRow>();
        Set(row, "actionLabel", actionLabel);
        Set(row, "keyLabel", keyButton.GetComponentInChildren<TMP_Text>());
        Set(row, "button", keyButton);

        var reset = UIButton("ResetKeys", panel.transform, "Reset Keys", new Vector2(-200, -370), new Vector2(320, 70));
        var back = UIButton("Back", panel.transform, "Back", new Vector2(200, -370), new Vector2(320, 70));

        var options = panel.gameObject.AddComponent<OptionsPanel>();
        Set(options, "musicSlider", music);
        Set(options, "sfxSlider", sfx);
        Set(options, "keybindContainer", list);
        Set(options, "rowTemplate", row);
        Set(options, "resetKeysButton", reset);
        Set(options, "backButton", back);
        panel.gameObject.SetActive(false);
        return options;
    }

    /// <summary>Shared sound library (created with the expected names, clips empty - drag yours in).</summary>
    internal static SoundLibrary GetSoundLibrary()
    {
        const string path = "Assets/Audio/SoundLibrary.asset";
        var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(path);
        if (lib != null) return lib;
        System.IO.Directory.CreateDirectory("Assets/Audio");
        lib = ScriptableObject.CreateInstance<SoundLibrary>();
        foreach (var n in new[] { "click", "jump", "switch", "ability_timestop", "timestop_ready", "portal", "teleport",
                                  "door_open", "door_close", "console", "checkpoint", "spotted", "caught", "level_complete",
                                  "bgm_menu", "bgm_game", "intro" })
            lib.sounds.Add(new SoundLibrary.Entry { name = n });
        AssetDatabase.CreateAsset(lib, path);
        return lib;
    }

    internal static SoundManager AddSoundManager(string sceneBgm)
    {
        var sm = new GameObject("SoundManager").AddComponent<SoundManager>();
        Set(sm, "library", GetSoundLibrary());
        var so = new SerializedObject(sm);
        so.FindProperty("sceneBGM").stringValue = sceneBgm;
        so.ApplyModifiedPropertiesWithoutUndo();
        return sm;
    }
}
