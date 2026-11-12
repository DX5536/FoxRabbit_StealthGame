using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Tools > FoxRabbit > Build Menu Scene
/// Creates Assets/Scenes/Menu.unity (Start / Options / Credits) and puts it first in the build list.
/// Set MenuController.startSceneIndex to your first game scene.
/// </summary>
public static partial class TestSceneBuilder
{
    const string MenuScenePath = "Assets/Scenes/Menu.unity";

    [MenuItem("Tools/FoxRabbit/Build Menu Scene")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(MenuScenePath) &&
            !EditorUtility.DisplayDialog("Build Menu Scene", $"{MenuScenePath} already exists. Overwrite it?", "Overwrite", "Cancel"))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.12f, 0.17f);
        cam.transform.position = new Vector3(0, 0, -10);
        cam.gameObject.AddComponent<AudioListener>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        AddSoundManager("bgm_menu");

        var canvas = UICanvas("Menu_Canvas", 0).transform;
        var menu = canvas.gameObject.AddComponent<MenuController>();

        // Main
        var main = UIRect("MainPanel", canvas, Vector2.zero, new Vector2(1920, 1080));
        UIText("Title", main, "FOX & RABBIT", new Vector2(0, 260), new Vector2(1400, 180), 120);
        UIText("Subtitle", main, "a stealth game", new Vector2(0, 160), new Vector2(1000, 60), 40);
        UIButton("StartButton", main, "Start Game", new Vector2(0, 0), new Vector2(420, 90), menu.StartGame);
        UIButton("OptionsButton", main, "Options", new Vector2(0, -110), new Vector2(420, 90), menu.OpenOptions);
        UIButton("CreditsButton", main, "Credits", new Vector2(0, -220), new Vector2(420, 90), menu.OpenCredits);

        // Options
        var options = BuildOptionsPanel(canvas);
        UnityEventTools.AddPersistentListener(options.onClosed, menu.ShowMain);

        // Credits (edit the text in the scene)
        var credits = UIPanel("CreditsPanel", canvas, Vector2.zero, new Vector2(1100, 900), PanelColor);
        UIText("Title", credits.transform, "CREDITS", new Vector2(0, 380), new Vector2(800, 80), 56);
        UIText("Body", credits.transform,
            "<b>Team</b>\nYour names here\n\n" +
            "<b>Tools & Assets</b>\n" +
            "Unity 6 + URP 2D, Cinemachine, Input System\n" +
            "DOTween & DOTween Pro - Demigiant\n" +
            "Yarn Spinner - Secret Lab\n" +
            "All In 1 Sprite Shader - Seaside Studios\n" +
            "Text Animator for Unity - Febucci\n\n" +
            "<b>Art / Music / SFX</b>\nAdd every asset you used and its license here",
            new Vector2(0, 0), new Vector2(950, 640), 32);
        UIButton("Back", credits.transform, "Back", new Vector2(0, -370), new Vector2(320, 70), menu.ShowMain);
        credits.gameObject.SetActive(false);

        Set(menu, "mainPanel", main.gameObject);
        Set(menu, "optionsPanel", options);
        Set(menu, "creditsPanel", credits.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);

        // Menu goes first: the web build opens it.
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == MenuScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[TestSceneBuilder] Built {MenuScenePath} (build index 0). Set MenuController > Start Scene Index to your first level.");
    }
}
