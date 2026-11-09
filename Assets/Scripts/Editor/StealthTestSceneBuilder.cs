using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > FoxRabbit > Build Stealth Test Scene
/// Creates Assets/Scenes/StealthTestLevel.unity (TestLevel is kept): patrolling guards, a rotating ceiling camera,
/// cover that blocks vision, parkour sections, a goal, and the game over popup (R to restart).
/// </summary>
public static partial class TestSceneBuilder
{
    const string StealthScenePath = "Assets/Scenes/StealthTestLevel.unity";

    static EnemyVisionProfile guardVision, cameraVision, sentryVision;
    static EnemyMovementProfile guardMove, cameraMove;

    [MenuItem("Tools/FoxRabbit/Build Stealth Test Scene")]
    public static void BuildStealth()
    {
        BuildScene(StealthScenePath, BuildStealthLevel, AddStealthExtras, new Vector2(-9f, 1f), new Vector2(-7.5f, 1f));
    }

    static void BuildStealthLevel()
    {
        CreateProfiles();

        var level = new GameObject("> Level").transform;
        var enemies = new GameObject("> Enemies").transform;
        Color ground = new Color(0.3f, 0.36f, 0.42f);
        Color platform = new Color(0.45f, 0.52f, 0.58f);
        Color wall = new Color(0.22f, 0.26f, 0.3f);
        Color cover = new Color(0.4f, 0.33f, 0.28f);

        // ===== A: Start (safe) =====
        Solid("Ground_A", level, new Vector2(-2f, -1f), new Vector2(24f, 2f), ground);
        Solid("Wall_Left", level, new Vector2(-12.5f, 6f), new Vector2(1f, 16f), wall);
        Label("Sign_Start", level, new Vector2(-6f, 4.5f),
            "Stay out of the vision cones!\nCover blocks sight. Both characters can be spotted.\nTetra F = stop time (enemies blind & frozen)\nPricilla F = portal, E = teleport");

        // ===== B: Guard patrol under platforms =====
        Solid("Ground_B", level, new Vector2(18f, -1f), new Vector2(16f, 2f), ground);
        Solid("Plat_B1", level, new Vector2(10.5f, 2f), new Vector2(2.5f, 0.5f), platform);
        Solid("Plat_B2", level, new Vector2(14f, 4.2f), new Vector2(3f, 0.5f), platform);
        Solid("Plat_B3", level, new Vector2(18.5f, 4.6f), new Vector2(3f, 0.5f), platform);
        Solid("Plat_B4", level, new Vector2(23f, 4.2f), new Vector2(3f, 0.5f), platform);
        Solid("Cover_B", level, new Vector2(12f, 0.9f), new Vector2(0.8f, 1.8f), cover); // low cover, hide behind it
        Label("Sign_B", level, new Vector2(18.5f, 7.2f), "Go over the guard - or stop time and walk past");

        var guard1 = CreateGuard("Guard_1", enemies, new Vector2(19f, 1f), new[] { 13.5f, 24f }, guardVision, -8f);

        // ===== C: Camera hall with pillars =====
        Solid("Ground_C", level, new Vector2(35f, -1f), new Vector2(18f, 2f), ground);
        Solid("Ceiling_C", level, new Vector2(35f, 8.25f), new Vector2(18f, 0.5f), wall);
        Solid("Pillar_C1", level, new Vector2(31.5f, 1.25f), new Vector2(1f, 2.5f), cover);
        Solid("Pillar_C2", level, new Vector2(38.5f, 1.25f), new Vector2(1f, 2.5f), cover);
        Solid("Plat_C", level, new Vector2(35f, 3.6f), new Vector2(2.5f, 0.4f), platform); // shadow right under the camera
        Label("Sign_C", level, new Vector2(35f, 6.5f), "Camera: wait for it to look away.\nPillars cast blind spots.");
        CreateCamera("SecurityCamera_1", enemies, new Vector2(35f, 7.7f), new List<float> { -50f, -130f }, cameraVision);

        // ===== D: Parkour + sentry on a ledge =====
        Solid("Pit_Floor_D", level, new Vector2(47.5f, -6f), new Vector2(7f, 2f), ground);
        Solid("Pit_Step_D", level, new Vector2(45.5f, -3.4f), new Vector2(2f, 0.4f), platform);
        Solid("Pit_Step_D2", level, new Vector2(49.5f, -2.2f), new Vector2(1.6f, 0.4f), platform);
        Solid("Ground_D", level, new Vector2(60f, -1f), new Vector2(18f, 2f), ground);
        Solid("Tall_Wall_D", level, new Vector2(56f, 3f), new Vector2(1.5f, 6f), wall);
        Solid("Step_D1", level, new Vector2(52.5f, 1.8f), new Vector2(2f, 0.4f), platform);
        Solid("Step_D2", level, new Vector2(54f, 4f), new Vector2(1.4f, 0.4f), platform);
        Solid("Ledge_D", level, new Vector2(62f, 5.8f), new Vector2(10f, 0.5f), platform);
        Solid("Ledge_Cover_D", level, new Vector2(59.2f, 6.75f), new Vector2(0.6f, 1.4f), cover);
        Label("Sign_D", level, new Vector2(48f, 3f), "Sprint-jump the gap (Shift)");

        CreateGuard("Sentry_Ledge", enemies, new Vector2(64f, 7.05f), new[] { 60.5f, 66f }, sentryVision, -25f);

        // ===== E: Goal =====
        Solid("Ground_E", level, new Vector2(76f, -1f), new Vector2(14f, 2f), ground);
        Solid("Wall_Right", level, new Vector2(83.5f, 6f), new Vector2(1f, 16f), wall);
        var goal = new GameObject("Goal");
        goal.transform.SetParent(level);
        goal.transform.position = new Vector2(80f, 1.5f);
        goal.transform.localScale = new Vector3(1.5f, 3f, 1f);
        var goalSr = goal.AddComponent<SpriteRenderer>();
        goalSr.sprite = square;
        goalSr.color = new Color(0.4f, 1f, 0.55f, 0.35f);
        goalSr.sortingOrder = 2;
        goal.AddComponent<BoxCollider2D>().isTrigger = true;
        goal.AddComponent<LevelGoal>();
        Label("Sign_Goal", level, new Vector2(80f, 4f), "EXIT");
    }

    static void AddStealthExtras(Context ctx)
    {
        new GameObject("GameOver_Manager").AddComponent<GameOver_Manager>();

        // Wider view for stealth
        var lens = ctx.vcam.Lens;
        lens.OrthographicSize = 7f;
        ctx.vcam.Lens = lens;
        ctx.camera.orthographicSize = 7f;
    }

    #region Enemies

    static GameObject CreateGuard(string name, Transform parent, Vector2 pos, float[] patrolX, EnemyVisionProfile vision, float lookAngle)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = capsule;
        sr.color = new Color(0.75f, 0.3f, 0.3f);
        sr.sortingOrder = 90;

        float top = sr.sprite.bounds.max.y;
        var eye = new GameObject("Eye").transform;
        eye.SetParent(go.transform, false);
        eye.localPosition = new Vector3(0f, top * 0.7f, 0f);

        var alert = AlertIcon(go.transform, top + 0.5f);

        var visionComp = go.AddComponent<EnemyVision_Manager>();
        Set(visionComp, "profile", vision);
        Set(visionComp, "eye", eye);
        Set(visionComp, "lookAngle", lookAngle);
        Set(visionComp, "alertIcon", alert);

        var points = new List<Object>();
        for (int i = 0; i < patrolX.Length; i++)
        {
            var p = new GameObject($"{name}_Point_{i}");
            p.transform.SetParent(parent);
            p.transform.position = new Vector2(patrolX[i], pos.y);
            points.Add(p.transform);
        }

        var move = go.AddComponent<EnemyMovement_Manager>();
        Set(move, "movementType", (int)EnemyMovement_Manager.MovementType.Patrol);
        Set(move, "profile", guardMove);
        Set(move, "patrolLookAngle", lookAngle);
        SetArray(move, "patrolPoints", points.ToArray());
        return go;
    }

    static GameObject CreateCamera(string name, Transform parent, Vector2 pos, List<float> angles, EnemyVisionProfile vision)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var mountVisual = new GameObject("Mount");
        mountVisual.transform.SetParent(go.transform, false);
        mountVisual.transform.localScale = new Vector3(0.6f, 0.3f, 1f);
        var mountSr = mountVisual.AddComponent<SpriteRenderer>();
        mountSr.sprite = square;
        mountSr.color = new Color(0.2f, 0.2f, 0.22f);
        mountSr.sortingOrder = 90;

        var head = new GameObject("Head").transform;
        head.SetParent(go.transform, false);
        head.localPosition = new Vector3(0f, -0.2f, 0f);
        var lensGo = new GameObject("Lens");
        lensGo.transform.SetParent(head, false);
        lensGo.transform.localPosition = new Vector3(0.3f, 0f, 0f);
        lensGo.transform.localScale = new Vector3(0.6f, 0.3f, 1f);
        var lensSr = lensGo.AddComponent<SpriteRenderer>();
        lensSr.sprite = square;
        lensSr.color = new Color(0.55f, 0.55f, 0.6f);
        lensSr.sortingOrder = 91;

        var eye = new GameObject("Eye").transform;
        eye.SetParent(head, false);
        eye.localPosition = new Vector3(0.6f, 0f, 0f);

        var alert = AlertIcon(go.transform, 0.6f);

        var visionComp = go.AddComponent<EnemyVision_Manager>();
        Set(visionComp, "profile", vision);
        Set(visionComp, "eye", eye);
        Set(visionComp, "lookAngle", angles[0]);
        Set(visionComp, "alertIcon", alert);

        var move = go.AddComponent<EnemyMovement_Manager>();
        Set(move, "movementType", (int)EnemyMovement_Manager.MovementType.RotateInPlace);
        Set(move, "profile", cameraMove);
        Set(move, "rotatingVisual", head);
        var so = new SerializedObject(move);
        var list = so.FindProperty("lookAngles");
        list.arraySize = angles.Count;
        for (int i = 0; i < angles.Count; i++) list.GetArrayElementAtIndex(i).floatValue = angles[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        head.rotation = Quaternion.Euler(0f, 0f, angles[0]);
        return go;
    }

    static GameObject AlertIcon(Transform parent, float height)
    {
        var go = new GameObject("AlertIcon");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = "!";
        tmp.fontSize = 6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.85f, 0.2f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(1f, 1f);
        tmp.sortingOrder = 95;
        go.SetActive(false);
        return go;
    }

    #endregion

    static void CreateProfiles()
    {
        int obstacles = (1 << groundLayer) | 1; // Ground + Default

        guardVision = Profile<EnemyVisionProfile>("Vision_Guard");
        guardVision.radius = 6f;
        guardVision.visionAngle = 60f;
        guardVision.timeToDetect = 0.35f;
        guardVision.obstacleMask = obstacles;

        sentryVision = Profile<EnemyVisionProfile>("Vision_Sentry");
        sentryVision.radius = 8f;
        sentryVision.visionAngle = 45f;
        sentryVision.timeToDetect = 0.5f;
        sentryVision.obstacleMask = obstacles;

        cameraVision = Profile<EnemyVisionProfile>("Vision_Camera");
        cameraVision.radius = 8.5f;
        cameraVision.visionAngle = 35f;
        cameraVision.proximityRadius = 0f;
        cameraVision.timeToDetect = 0.2f;
        cameraVision.obstacleMask = obstacles;
        cameraVision.idleColor = new Color(0.6f, 0.9f, 1f, 0.18f);

        guardMove = Profile<EnemyMovementProfile>("Movement_Guard");
        guardMove.moveSpeed = 2f;
        guardMove.waitDuration = 1.5f;
        guardMove.turnDuration = 0.3f;
        guardMove.moveEase = Ease.InOutSine;

        cameraMove = Profile<EnemyMovementProfile>("Movement_Camera");
        cameraMove.rotateDuration = 1.6f;
        cameraMove.rotateWaitDuration = 1.2f;

        foreach (var p in new ScriptableObject[] { guardVision, sentryVision, cameraVision, guardMove, cameraMove })
            EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
    }

    static T Profile<T>(string name) where T : ScriptableObject
    {
        string path = $"{AssetFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
