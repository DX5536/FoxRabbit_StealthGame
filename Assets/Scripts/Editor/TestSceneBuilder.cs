using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Tools > FoxRabbit > Build Test Scene
/// Creates Assets/Scenes/TestLevel.unity with a small test level, both characters fully wired
/// (movement, switching, time stop, portals), Cinemachine follow camera, screen filters and a controls overlay.
/// Placeholder art only - swap sprites afterwards.
/// </summary>
public static partial class TestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/TestLevel.unity";
    internal const string AssetFolder = "Assets/Scenes/TestLevel_Assets";
    const string GroundLayer = "Ground";

    internal static Sprite square;
    internal static Sprite capsule;
    internal static int groundLayer;

    internal class Context
    {
        public Camera camera;
        public CinemachineCamera vcam;
        public TimeStopFilter_Manager filter;
        public GlassBreakFilter glass;
        public PlayerCharacter tetra;
        public PlayerCharacter pricilla;
    }

    [MenuItem("Tools/FoxRabbit/Build Test Scene")]
    public static void Build() => BuildScene(ScenePath, BuildLevel, null, new Vector2(-2f, 1f), new Vector2(-0.8f, 1f));

    internal static void BuildScene(string scenePath, System.Action buildLevel, System.Action<Context> extras, Vector2 tetraPos, Vector2 pricillaPos)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(scenePath) &&
            !EditorUtility.DisplayDialog("Build Test Scene", $"{scenePath} already exists. Overwrite it?", "Overwrite", "Cancel"))
            return;

        ScreenFilterEditorTools.CreateTagsAndLayer();
        groundLayer = ScreenFilterEditorTools.EnsureLayer(GroundLayer);
        if (groundLayer < 0) return;

        Directory.CreateDirectory(AssetFolder);
        square = CreateSquareSprite();
        capsule = LoadPackageSprite("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Capsule.png") ?? square;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------- Camera & light ----------
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.2f, 0.27f);
        camGo.transform.position = new Vector3(0, 2, -10);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<CinemachineBrain>();
        cam.GetUniversalAdditionalCameraData();

        var lightGo = new GameObject("Global Light 2D");
        var light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        var vcamGo = new GameObject("CM Follow Camera");
        vcamGo.transform.position = new Vector3(0, 2, -10);
        var vcam = vcamGo.AddComponent<CinemachineCamera>();
        var lens = vcam.Lens;
        lens.OrthographicSize = 6f;
        vcam.Lens = lens;
        var composer = vcamGo.AddComponent<CinemachinePositionComposer>();
        composer.CameraDistance = 10f;
        composer.Damping = new Vector3(0.5f, 0.7f, 0f);
        composer.Lookahead.Enabled = true;
        composer.Lookahead.Time = 0.25f;
        composer.Lookahead.Smoothing = 8f;
        composer.Lookahead.IgnoreY = true;
        var comp = composer.Composition;
        comp.ScreenPosition = new Vector2(0f, -0.1f);
        comp.DeadZone.Enabled = true;
        comp.DeadZone.Size = new Vector2(0.1f, 0.15f);
        composer.Composition = comp;

        // ---------- Level ----------
        buildLevel();

        // ---------- Managers ----------
        var managers = new GameObject("> GameManager");
        var filterGo = new GameObject("TimeStopFilter_Manager");
        filterGo.transform.SetParent(managers.transform);
        var filter = filterGo.AddComponent<TimeStopFilter_Manager>();
        Set(filter, "mainCamera", cam);

        var glassGo = new GameObject("GlassBreak");
        glassGo.transform.SetParent(managers.transform);
        var glass = glassGo.AddComponent<GlassBreakFilter>();
        Set(glass, "crackTexture", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/ScreenFilter_2DSprites/GlassBreak_1920x1080px_Shader.png"));
        Set(glass, "directionMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/ScreenFilter_2DSprites/GlassBreak_1920x1080px_Shader_DirectionMap.png"));

        // ---------- Players ----------
        var playerRoot = new GameObject("> Player");
        var portal0 = CreatePortalPrefab("Portal_0", new Color(0.35f, 0.75f, 1f));
        var portal1 = CreatePortalPrefab("Portal_1", new Color(1f, 0.6f, 0.25f));

        var tetra = CreateCharacter("Player_Tetra", "Tetra", new Color(0.4f, 0.45f, 1f), tetraPos, playerRoot.transform);
        var pricilla = CreateCharacter("Player_Pricilla", "Pricilla", new Color(0.9f, 0.35f, 0.45f), pricillaPos, playerRoot.transform);

        // Tetra: hourglass + time stop
        var hourglassSprite = LoadFirstSprite("Assets/UI/Hourglass_350x500.png");
        var hgGo = new GameObject("Hourglass");
        hgGo.transform.SetParent(tetra.transform, false);
        hgGo.transform.localPosition = new Vector3(0.75f, 0.7f, 0);
        var hgSr = hgGo.AddComponent<SpriteRenderer>();
        hgSr.sprite = hourglassSprite != null ? hourglassSprite : square;
        hgSr.color = new Color(0.75f, 0.85f, 1f, 0.9f);
        hgSr.sortingOrder = 5;
        float hgHeight = hgSr.sprite.bounds.size.y;
        hgGo.transform.localScale = Vector3.one * (0.6f / Mathf.Max(hgHeight, 0.01f));
        var hourglass = hgGo.AddComponent<HourglassFill>();

        var timeStop = tetra.gameObject.AddComponent<TimeStop_Manager>();
        Set(timeStop, "filter", filter);
        Set(timeStop, "hourglass", hourglass);
        Set(timeStop, "cooldownDuration", 6f);
        Set(timeStop, "timeStopDuration", 4f);
        Set(tetra, "ability", timeStop);

        // Pricilla: 2 charge bars + teleport
        var bar0 = CreateBlockSprite("PortalBar_0", pricilla.transform, new Vector2(0.65f, 0.55f), new Vector2(0.12f, 0.45f), new Color(0.35f, 0.75f, 1f), 5);
        var bar1 = CreateBlockSprite("PortalBar_1", pricilla.transform, new Vector2(0.85f, 0.55f), new Vector2(0.12f, 0.45f), new Color(1f, 0.6f, 0.25f), 5);

        var teleport = pricilla.gameObject.AddComponent<Teleportation_Manager>();
        SetArray(teleport, "portalPrefabs", portal0, portal1);
        SetArray(teleport, "chargeBars", bar0, bar1);
        Set(pricilla, "ability", teleport);

        // Controller
        var controller = playerRoot.AddComponent<PlayerController>();
        SetArray(controller, "characters", tetra, pricilla);
        Set(controller, "followCamera", vcam);
        vcam.Follow = tetra.transform;
        vcamGo.transform.position = new Vector3(tetraPos.x, tetraPos.y, -10f);

        // ---------- UI ----------
        BuildUI(glass, filter);

        extras?.Invoke(new Context { camera = cam, vcam = vcam, filter = filter, glass = glass, tetra = tetra, pricilla = pricilla });

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        AddToBuildSettings(scenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TestSceneBuilder] Built {scenePath}. Press Play to test.");
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, false)); // listed but unticked: test levels never ship
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    #region Level

    static void BuildLevel()
    {
        var level = new GameObject("> Level").transform;
        Color ground = new Color(0.32f, 0.42f, 0.36f);
        Color platform = new Color(0.45f, 0.55f, 0.5f);
        Color wall = new Color(0.25f, 0.3f, 0.33f);

        // Start area
        Solid("Ground_Start", level, new Vector2(4f, -1f), new Vector2(32f, 2f), ground);
        Solid("Wall_Left", level, new Vector2(-12.5f, 5f), new Vector2(1f, 14f), wall);

        // Staircase of platforms (jump test)
        // Rules used for every test level: max step up 1.9 (jump is 2.6), walk-jump gap <= 2.5, 2.5+ headroom under platforms.
        Solid("Platform_1", level, new Vector2(5f, 0.8f), new Vector2(3f, 1.6f), platform);   // box, top 1.6
        Solid("Platform_2", level, new Vector2(9f, 3.2f), new Vector2(3f, 0.5f), platform);   // top 3.45
        Solid("Platform_3", level, new Vector2(13f, 4.8f), new Vector2(3f, 0.5f), platform);  // top 5.05

        // Sprint gap (4.5 units: walking jump ~3.7, sprint jump ~5.5) with a safe pit and steps back up
        Solid("Pit_Floor", level, new Vector2(22.25f, -6f), new Vector2(4.5f, 2f), ground);
        Solid("Pit_Step_1", level, new Vector2(21f, -3.5f), new Vector2(1.6f, 0.5f), platform);   // top -3.25
        Solid("Pit_Step_2", level, new Vector2(23.3f, -1.6f), new Vector2(1.4f, 0.5f), platform); // top -1.35
        Solid("Ground_AfterGap", level, new Vector2(36.25f, -1f), new Vector2(23.5f, 2f), ground);

        // Tall wall + ledge: the long way round, a good spot to test portals as a shortcut back
        Solid("Tall_Wall", level, new Vector2(44f, 2f), new Vector2(2f, 4f), wall);                 // top 4
        Solid("Ledge_Step_1", level, new Vector2(39.5f, 0.8f), new Vector2(2f, 1.6f), platform);    // box, top 1.6
        Solid("Ledge_Step_2", level, new Vector2(42f, 2.8f), new Vector2(1.6f, 0.5f), platform);    // top 3.05
        Solid("Ground_Far", level, new Vector2(54f, -1f), new Vector2(20f, 2f), ground);
        Solid("Wall_Right", level, new Vector2(64.5f, 5f), new Vector2(1f, 14f), wall);

        // Time stop test: moving crushers that freeze while time is stopped
        for (int i = 0; i < 3; i++)
        {
            var crusher = Solid($"TimeStop_Crusher_{i}", level, new Vector2(50f + i * 4f, 4.5f), new Vector2(1.5f, 1.5f), new Color(0.85f, 0.3f, 0.3f));
            var mover = crusher.AddComponent<TimeStopMover>();
            Set(mover, "moveOffset", new Vector2(0f, -4f));
            Set(mover, "period", 1.4f + i * 0.35f);
            var rb = crusher.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // Signs
        Label("Sign_Jump", level, new Vector2(5f, 6f), "Jump: Space / W");
        Label("Sign_Sprint", level, new Vector2(21f, 3f), "Sprint: Shift\nto clear the gap");
        Label("Sign_Portal", level, new Vector2(38f, 7.5f), "Pricilla: F = place portal\nE = teleport, F in portal = pick up");
        Label("Sign_TimeStop", level, new Vector2(54f, 8f), "Tetra: F = stop time\n(crushers freeze)");
    }

    internal static GameObject Solid(string name, Transform parent, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(name) { layer = groundLayer };
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = 0;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }

    internal static void Label(string name, Transform parent, Vector2 pos, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 1f, 1f, 0.8f);
        tmp.rectTransform.sizeDelta = new Vector2(10f, 3f);
        tmp.sortingOrder = 1;
    }

    #endregion

    #region Characters & portals

    static PlayerCharacter CreateCharacter(string objName, string displayName, Color color, Vector2 pos, Transform parent)
    {
        var go = new GameObject(objName) { tag = "Tetra" }; // both excluded from the time stop filter, like your scene
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = capsule;
        sr.color = color;
        sr.sortingOrder = 100;

        var character = go.AddComponent<PlayerCharacter>(); // adds Rigidbody2D + CapsuleCollider2D
        var col = go.GetComponent<CapsuleCollider2D>();
        Bounds b = sr.sprite.bounds;
        col.size = new Vector2(b.size.x * 0.9f, b.size.y * 0.98f);
        col.offset = b.center;
        col.direction = b.size.y >= b.size.x ? CapsuleDirection2D.Vertical : CapsuleDirection2D.Horizontal;
        var rb = go.GetComponent<Rigidbody2D>();
        rb.gravityScale = 3f;

        character.characterName = displayName;
        Set(character, "groundLayers", (LayerMask)(1 << groundLayer));
        Set(character, "jumpHeight", 2.6f);

        // Selection arrow
        var arrow = new GameObject("SelectionIndicator");
        arrow.transform.SetParent(go.transform, false);
        arrow.transform.localPosition = new Vector3(0f, b.max.y + 0.45f, 0f);
        var diamond = new GameObject("Diamond");
        diamond.transform.SetParent(arrow.transform, false);
        diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        diamond.transform.localScale = new Vector3(0.25f, 0.25f, 1f);
        var arrowSr = diamond.AddComponent<SpriteRenderer>();
        arrowSr.sprite = square;
        arrowSr.color = Color.Lerp(color, Color.white, 0.4f);
        arrowSr.sortingOrder = 6;
        Set(character, "selectionIndicator", arrow.transform);
        return character;
    }

    static GameObject CreatePortalPrefab(string name, Color color)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = capsule;
        sr.color = new Color(color.r, color.g, color.b, 0.75f);
        sr.sortingOrder = 50;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(sr.sprite.bounds.size.x * 0.8f, sr.sprite.bounds.size.y);
        box.offset = sr.sprite.bounds.center;
        var portal = go.AddComponent<Portal>();
        portal.exitOffset = Vector2.zero;

        float top = sr.sprite.bounds.max.y;
        portal.teleportPrompt = PromptText("Prompt_E", go.transform, new Vector2(-0.35f, top + 0.4f), "[E]");
        portal.recallPrompt = PromptText("Prompt_F", go.transform, new Vector2(0.35f, top + 0.4f), "[F]");

        string path = $"{AssetFolder}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject PromptText(string name, Transform parent, Vector2 pos, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(1f, 0.6f);
        tmp.sortingOrder = 60;
        go.SetActive(false);
        return go;
    }

    static SpriteRenderer CreateBlockSprite(string name, Transform parent, Vector2 localPos, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    #endregion

    #region UI

    static void BuildUI(GlassBreakFilter glass, TimeStopFilter_Manager filter)
    {
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var canvasGo = new GameObject("UI_Canvas (Overlay - not filtered)");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var help = new GameObject("Controls");
        help.transform.SetParent(canvasGo.transform, false);
        var text = help.AddComponent<TextMeshProUGUI>();
        text.text = "<b>Controls</b>\nA/D move   Space jump   Shift sprint\nQ switch   F ability   E interact";
        text.fontSize = 28;
        text.color = new Color(1, 1, 1, 0.85f);
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30, -30);
        rt.sizeDelta = new Vector2(700, 140);

        CreateButton(canvasGo.transform, "DEBUG_GlassBreak", "Glass Break (2s)", new Vector2(-30, -30), b =>
            UnityEventTools.AddFloatPersistentListener(b.onClick, glass.BreakFor, 2f));
        CreateButton(canvasGo.transform, "DEBUG_TimeStopFilter", "Filter Only (3s)", new Vector2(-30, -110), b =>
            UnityEventTools.AddFloatPersistentListener(b.onClick, filter.ActivateFor, 3f));
    }

    static void CreateButton(Transform parent, string name, string label, Vector2 pos, System.Action<Button> wire)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.9f);
        var button = go.AddComponent<Button>();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(280, 60);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.color = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;
        var trt = tmp.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        wire(button);
    }

    #endregion

    #region Helpers

    static Sprite CreateSquareSprite()
    {
        string path = $"{AssetFolder}/WhiteSquare.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadPackageSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    internal static Sprite LoadFirstSprite(string path)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            if (obj is Sprite s) return s;
        return null;
    }

    internal static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[TestSceneBuilder] Field '{field}' not found on {target.GetType().Name}.");
            return;
        }

        switch (value)
        {
            case Object o: prop.objectReferenceValue = o; break;
            case float f: prop.floatValue = f; break;
            case int i when prop.propertyType == SerializedPropertyType.Enum: prop.enumValueIndex = i; break;
            case int i: prop.intValue = i; break;
            case Color col: prop.colorValue = col; break;
            case bool b: prop.boolValue = b; break;
            case Vector2 v: prop.vector2Value = v; break;
            case LayerMask m: prop.intValue = m.value; break;
            default: Debug.LogWarning($"[TestSceneBuilder] Unsupported type for '{field}'."); break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetArray(Object target, string field, params Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null || !prop.isArray)
        {
            Debug.LogWarning($"[TestSceneBuilder] Array '{field}' not found on {target.GetType().Name}.");
            return;
        }
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
}
