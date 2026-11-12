using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>Adds SoundManager (with gameplay sounds wired), Pause menu and the Yarn visual novel intro to a built level.</summary>
public static partial class TestSceneBuilder
{
    const string YarnProjectPath = "Assets/Dialogue/FoxRabbit.yarnproject";
    const string YarnPrefabPath = "Packages/dev.yarnspinner.unity/Prefabs/Dialogue System.prefab";

    internal static void AddGameSystems(Context ctx)
    {
        var sound = AddSoundManager("bgm_game");
        WireSounds(sound, ctx);
        BuildPauseMenu();
        BuildVisualNovel();
    }

    static void WireSounds(SoundManager sound, Context ctx)
    {
        var play = new UnityAction<string>(sound.PlaySFX);
        void Hook(UnityEventBase e, string soundName)
        {
            if (e == null) { Debug.LogWarning($"[TestSceneBuilder] Event for '{soundName}' is null, sound not wired."); return; }
            UnityEventTools.AddStringPersistentListener(e, play, soundName);
        }

        foreach (var c in new[] { ctx.tetra, ctx.pricilla })
        {
            Hook(c.onJump, "jump");
            Hook(c.onSelected, "switch");
        }
        if (ctx.tetra.TryGetComponent(out TimeStop_Manager timeStop))
        {
            Hook(timeStop.onTimeStopStarted, "ability_timestop");
            Hook(timeStop.onReady, "timestop_ready");
        }
        if (ctx.pricilla.TryGetComponent(out Teleportation_Manager teleport))
        {
            Hook(teleport.onAbilityUsed, "portal");
            Hook(teleport.onTeleported, "teleport");
        }
        foreach (var door in Object.FindObjectsByType<DoorBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Hook(door.onOpen, "door_open");
            Hook(door.onClose, "door_close");
        }
        foreach (var console in Object.FindObjectsByType<SecurityConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Hook(console.onInteract, "console");
        foreach (var vision in Object.FindObjectsByType<EnemyVision_Manager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Hook(vision.onPlayerSpotted, "spotted");
        foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Hook(cp.onActivated, "checkpoint");
            PrefabUtility.RecordPrefabInstancePropertyModifications(cp);
        }
        var gameOver = Object.FindFirstObjectByType<GameOver_Manager>();
        if (gameOver != null)
        {
            Hook(gameOver.onCaught, "caught");
            Hook(gameOver.onLevelComplete, "level_complete");
        }
    }

    static void BuildPauseMenu()
    {
        var canvas = UICanvas("Pause_Canvas", 500).transform;
        var pause = canvas.gameObject.AddComponent<PauseMenu>();
        var panel = UIPanel("PausePanel", canvas, Vector2.zero, new Vector2(620, 640), PanelColor);
        UIText("Title", panel.transform, "PAUSED", new Vector2(0, 230), new Vector2(500, 80), 56);
        UIButton("Resume", panel.transform, "Resume", new Vector2(0, 110), new Vector2(420, 80), pause.Resume);
        UIButton("Options", panel.transform, "Options", new Vector2(0, 10), new Vector2(420, 80), pause.OpenOptions);
        UIButton("Restart", panel.transform, "Restart (checkpoint)", new Vector2(0, -90), new Vector2(420, 80), pause.Restart);
        UIButton("MainMenu", panel.transform, "Main Menu", new Vector2(0, -190), new Vector2(420, 80), pause.MainMenu);

        var options = BuildOptionsPanel(canvas);
        UnityEventTools.AddPersistentListener(options.onClosed, pause.BackFromOptions);
        Set(pause, "pausePanel", panel.gameObject);
        Set(pause, "optionsPanel", options);
    }

    static void BuildVisualNovel()
    {
        var project = AssetDatabase.LoadAssetAtPath<YarnProject>(YarnProjectPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(YarnPrefabPath);
        if (project == null || prefab == null)
        {
            Debug.LogWarning($"[TestSceneBuilder] Visual novel skipped: missing {(project == null ? YarnProjectPath : YarnPrefabPath)}. " +
                             "Let Unity import Assets/Dialogue, then rebuild.");
            return;
        }

        // Yarn's ready-made dialogue UI (line + options). Unpacked so we can drop its extra EventSystem.
        var dialogue = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(dialogue, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var extraEventSystem = dialogue.GetComponentInChildren<EventSystem>(true);
        if (extraEventSystem != null) Object.DestroyImmediate(extraEventSystem.gameObject);
        foreach (var c in dialogue.GetComponentsInChildren<Canvas>(true))
            if (c.isRootCanvas) c.sortingOrder = 30; // above the VN stage (20), below pause (500)

        var runner = dialogue.GetComponentInChildren<DialogueRunner>(true);
        Set(runner, "yarnProject", project);
        Set(runner, "autoStart", false);
        var advancer = dialogue.GetComponentInChildren<LineAdvancer>(true);
        if (advancer != null)
        {
            // Escape is the pause key; next line = Enter (Space still hurries / continues, mouse click works too).
            var so = new SerializedObject(advancer);
            var next = so.FindProperty("nextLineKeyCode");
            if (next != null) { next.intValue = (int)KeyCode.Return; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        // Stage: background + 3 portrait slots
        var stageCanvas = UICanvas("VN_Stage_Canvas", 20);
        var stage = stageCanvas.gameObject.AddComponent<CanvasGroup>();
        var bg = UIPanel("Background", stageCanvas.transform, Vector2.zero, new Vector2(1920, 1080), Color.white);
        Stretch(bg.rectTransform);
        Image Slot(string name, float x)
        {
            var img = UIPanel(name, stageCanvas.transform, new Vector2(x, -120), new Vector2(420, 760), Color.white);
            img.preserveAspect = true;
            return img;
        }
        var left = Slot("Slot_Left", -560f);
        var center = Slot("Slot_Center", 0f);
        var right = Slot("Slot_Right", 560f);

        var vn = new GameObject("VisualNovel_Manager").AddComponent<VisualNovel_Manager>();
        Set(vn, "runner", runner);
        Set(vn, "stage", stage);
        Set(vn, "background", bg);
        Set(vn, "leftSlot", left);
        Set(vn, "centerSlot", center);
        Set(vn, "rightSlot", right);
        SetArray(vn, "characters", VNCharacterAsset("Tetra", new Color(0.4f, 0.45f, 1f)), VNCharacterAsset("Pricilla", new Color(0.9f, 0.35f, 0.45f)));
        SetArray(vn, "backgrounds", PlaceholderBackground("lab", new Color(0.12f, 0.16f, 0.22f), new Color(0.25f, 0.32f, 0.38f)));
    }

    static VNCharacter VNCharacterAsset(string id, Color tint) => Profile<VNCharacter>("VN_" + id, c =>
    {
        c.id = id;
        c.portraitTint = tint;
        foreach (var expression in new[] { "neutral", "happy", "mad" })
            c.expressions.Add(new VNCharacter.Expression { name = expression, sprite = capsule });
    });

    /// <summary>Gradient placeholder background; the sprite name is what <<bg name>> uses.</summary>
    static Sprite PlaceholderBackground(string name, Color bottom, Color top)
    {
        string path = $"{AssetFolder}/{name}.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(64, 36, TextureFormat.RGBA32, false);
            for (int y = 0; y < 36; y++)
                for (int x = 0; x < 64; x++) tex.SetPixel(x, y, Color.Lerp(bottom, top, y / 35f));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
