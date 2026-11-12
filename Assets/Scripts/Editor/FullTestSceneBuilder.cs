using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > FoxRabbit > Build Full Test Scene
/// Creates Assets/Scenes/FullTestLevel.unity (other test scenes are kept) with every feature so far:
///   1. Guard under jump platforms          -> Checkpoint 1
///   2. Swinging door + guard + hiding closet
///   3. Timed security door (console before it, time stop pauses the countdown)
///   4. Camera room, platform console opens a toggle security door  -> Checkpoint 2
///   5. Sprint gap, wall climb, sentry ledge, crushers that freeze in time stop
///   6. Exit (both characters)
/// Layout rules: step up <= 1.9 (jump 2.6), walking gaps <= 2.5, 2.5+ headroom, sprint gap 4.5.
/// </summary>
public static partial class TestSceneBuilder
{
    const string FullScenePath = "Assets/Scenes/FullTestLevel.unity";
    const float CeilingY = 9f; // ceiling bottom = 8.75

    [MenuItem("Tools/FoxRabbit/Build Full Test Scene")]
    public static void BuildFull()
    {
        BuildScene(FullScenePath, BuildFullLevel, AddFullExtras, new Vector2(-9f, 1f), new Vector2(-7.5f, 1f));
    }

    static void BuildFullLevel()
    {
        CreateProfiles();
        doorProfile = Profile<DoorProfile>("Door_Default", p => p.openTime = 5f);

        var level = new GameObject("> Level").transform;
        var doors = new GameObject("> Doors & Consoles").transform;
        var enemies = new GameObject("> Enemies").transform;
        var checkpoints = new GameObject("> Checkpoints").transform;
        Color ground = new Color(0.3f, 0.36f, 0.42f);
        Color platform = new Color(0.45f, 0.52f, 0.58f);
        Color wall = new Color(0.22f, 0.26f, 0.3f);
        Color cover = new Color(0.4f, 0.33f, 0.28f);

        // Shell
        Solid("Ceiling", level, new Vector2(50f, CeilingY), new Vector2(126f, 0.5f), wall);
        Solid("Wall_Left", level, new Vector2(-12.5f, 4f), new Vector2(1f, 10f), wall);
        Solid("Wall_Right", level, new Vector2(112.5f, 4f), new Vector2(1f, 10f), wall);

        // ===== 0. Start =====
        Solid("Ground_Start", level, new Vector2(-3f, -1f), new Vector2(18f, 2f), ground);
        Label("Sign_Start", level, new Vector2(-6f, 5f),
            "FULL TEST\nQ switch  F ability  E interact  Shift sprint / circuit\nCheckpoints save your progress for [R] restart");

        // ===== 1. Guard under platforms (x 6-26) =====
        Solid("Ground_1", level, new Vector2(16f, -1f), new Vector2(20f, 2f), ground);
        Solid("Box_1", level, new Vector2(8.5f, 0.8f), new Vector2(1.5f, 1.6f), cover);         // top 1.6
        Solid("Plat_1A", level, new Vector2(12f, 3.2f), new Vector2(3f, 0.5f), platform);       // top 3.45, gap 1.25
        Solid("Plat_1B", level, new Vector2(16.5f, 3.8f), new Vector2(3f, 0.5f), platform);     // top 4.05, gap 1.5
        Solid("Plat_1C", level, new Vector2(21f, 3.2f), new Vector2(3f, 0.5f), platform);       // top 3.45, gap 1.5
        CreateGuard("Guard_1", enemies, new Vector2(17f, 1f), new[] { 11f, 24f }, guardVision, -8f);
        Label("Sign_1", level, new Vector2(16.5f, 7f), "Over the guard, or stop time and walk past");

        // ===== Checkpoint 1 =====
        Solid("Ground_2", level, new Vector2(39f, -1f), new Vector2(26f, 2f), ground);          // x 26-52
        PlaceCheckpoint(checkpoints, new Vector2(28f, 1f));

        // ===== 2. Swinging door, corridor guard, hiding closet (x 32-48) =====
        CreateSimpleDoor("SimpleDoor_Corridor", doors, 32f);
        WallAbove(level, 32f, 3f, wall);
        Solid("Crate_2", level, new Vector2(36f, 0.6f), new Vector2(1.2f, 1.2f), cover);          // step to the closet ledge
        Solid("Closet_Ledge", level, new Vector2(40.5f, 2.5f), new Vector2(6f, 0.5f), platform); // x 37.5-43.5, top 2.75
        Solid("Closet_WallR", level, new Vector2(43.25f, 4.25f), new Vector2(0.5f, 3f), wall);
        Solid("Closet_Roof", level, new Vector2(41.5f, 6f), new Vector2(4f, 0.5f), wall);
        CreateSimpleDoor("SimpleDoor_Closet", doors, 39.5f, 2.75f);
        WallAbove(level, 39.5f, 6f, wall);
        CreateGuard("Guard_2", enemies, new Vector2(42f, 1f), new[] { 35f, 48f }, guardVision, -5f);
        Label("Sign_2", level, new Vector2(40f, 7.8f), "Closed doors block vision. Hide in the closet.");

        // ===== 3. Timed security door (x 49-52) =====
        var timedDoor = CreateSecurityDoor("SecurityDoor_Timed", "Timed Door", doors, 52f, SecurityDoor_Manager.DoorType.Timed);
        WallAbove(level, 52f, 3f, wall);
        CreateConsole("Console_Timed", "Console T", doors, 49.5f, new Color(1f, 0.55f, 0.15f), timedDoor);

        // ===== 4. Camera room + platform console -> toggle door (x 52-72) =====
        Solid("Ground_4", level, new Vector2(62f, -1f), new Vector2(20f, 2f), ground);          // x 52-72
        CreateCamera("Camera_4", enemies, new Vector2(58f, 8.4f), new System.Collections.Generic.List<float> { -60f, -120f }, cameraVision);
        Solid("SafePlat_4", level, new Vector2(58f, 3.6f), new Vector2(2.5f, 0.4f), platform);  // blind spot right under the camera
        Solid("Box_4", level, new Vector2(63f, 0.8f), new Vector2(1.5f, 1.6f), cover);          // top 1.6
        Solid("ConsolePlat_4", level, new Vector2(65.25f, 3.2f), new Vector2(3f, 0.5f), platform); // x 63.75-66.75, top 3.45
        var toggleDoor = CreateSecurityDoor("SecurityDoor_Toggle", "Toggle Door", doors, 70f, SecurityDoor_Manager.DoorType.Toggle);
        WallAbove(level, 70f, 3f, wall);
        CreateConsole("Console_Toggle", "Console B", doors, 65.5f, new Color(0.9f, 0.35f, 1f), toggleDoor)
            .transform.position = new Vector2(65.5f, 3.45f);
        Label("Sign_4", level, new Vector2(62f, 6.8f), "Timed door: 5s. Toggle door: console on the platform.\nHold [Shift] to see which console opens what.");

        // ===== Checkpoint 2 =====
        Solid("Ground_5", level, new Vector2(76f, -1f), new Vector2(8f, 2f), ground);           // x 72-80
        PlaceCheckpoint(checkpoints, new Vector2(74f, 1f));

        // ===== 5. Sprint gap, wall, sentry ledge, crushers (x 80-104) =====
        Solid("Pit_Floor", level, new Vector2(82.25f, -6f), new Vector2(4.5f, 2f), ground);     // gap 80-84.5
        Solid("Pit_Step_1", level, new Vector2(81f, -3.5f), new Vector2(1.6f, 0.5f), platform);   // top -3.25
        Solid("Pit_Step_2", level, new Vector2(83.3f, -1.6f), new Vector2(1.4f, 0.5f), platform); // top -1.35
        Solid("Ground_6", level, new Vector2(98.25f, -1f), new Vector2(27.5f, 2f), ground);     // x 84.5-112
        Solid("Step_5", level, new Vector2(87f, 1.6f), new Vector2(2f, 0.4f), platform);        // top 1.8
        Solid("Wall_5", level, new Vector2(90f, 1.75f), new Vector2(1.5f, 3.5f), wall);         // top 3.5
        Solid("Ledge_5", level, new Vector2(96f, 4.8f), new Vector2(10f, 0.5f), platform);      // x 91-101, top 5.05
        Solid("Ledge_Cover_5", level, new Vector2(93.2f, 5.75f), new Vector2(0.6f, 1.4f), cover);
        CreateGuard("Sentry_5", enemies, new Vector2(98f, 6.05f), new[] { 94.5f, 100f }, sentryVision, -25f);
        for (int i = 0; i < 2; i++)
        {
            var crusher = Solid($"Crusher_{i}", level, new Vector2(95f + i * 4f, 3.6f), new Vector2(1.2f, 1.2f), new Color(0.85f, 0.3f, 0.3f));
            var mover = crusher.AddComponent<TimeStopMover>();
            Set(mover, "moveOffset", new Vector2(0f, -3f));
            Set(mover, "period", 1.5f + i * 0.4f);
            crusher.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        }
        Label("Sign_5", level, new Vector2(86f, 6.5f), "Sprint-jump the gap. Climb over, or sneak under the ledge\n(crushers freeze during time stop).");

        // ===== 6. Exit (both characters) =====
        var goal = new GameObject("Goal");
        goal.transform.SetParent(level);
        goal.transform.position = new Vector2(108f, 1.5f);
        goal.transform.localScale = new Vector3(2f, 3f, 1f);
        var goalSr = goal.AddComponent<SpriteRenderer>();
        goalSr.sprite = square;
        goalSr.color = new Color(0.4f, 1f, 0.55f, 0.3f);
        goalSr.sortingOrder = 2;
        goal.AddComponent<BoxCollider2D>().isTrigger = true;
        var levelGoal = goal.AddComponent<LevelGoal>();
        Set(levelGoal, "requireBothCharacters", true);
        Label("Sign_Goal", level, new Vector2(108f, 4.5f), "EXIT - bring both");
    }

    static void AddFullExtras(Context ctx)
    {
        new GameObject("GameOver_Manager").AddComponent<GameOver_Manager>();
        new GameObject("CircuitView_Manager").AddComponent<CircuitView_Manager>();
        var lens = ctx.vcam.Lens;
        lens.OrthographicSize = 7f;
        ctx.vcam.Lens = lens;
        ctx.camera.orthographicSize = 7f;
        AddGameSystems(ctx); // SoundManager + sounds, Pause menu, visual novel intro
    }

    /// <summary>Wall from fromY up to the ceiling so nobody jumps over a door.</summary>
    static void WallAbove(Transform parent, float x, float fromY, Color color)
    {
        float top = CeilingY - 0.25f;
        Solid($"Wall_Above_{x}", parent, new Vector2(x, (fromY + top) * 0.5f), new Vector2(1f, top - fromY), color)
            .GetComponent<SpriteRenderer>().sortingOrder = 10;
    }
}
