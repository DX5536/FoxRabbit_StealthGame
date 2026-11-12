using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > FoxRabbit > Build Door Test Scene
/// Creates Assets/Scenes/DoorTestLevel.unity (other test scenes are kept).
///   Room A (Tetra start)  : console for Pricilla's timed door, simple door to the guarded corridor.
///   Corridor             : patrolling guard - a closed simple door blocks its vision.
///   Security Door A      : toggle door, opened from Pricilla's console.
///   Room C               : exit (both characters needed).
///   Security Door B      : timed door between Room C and Pricilla's room, opened from Tetra's console.
///   Room D (Pricilla)    : console for Door A, simple door (test portal priority here).
/// Hold [Shift] to see which console drives which door.
/// </summary>
public static partial class TestSceneBuilder
{
    const string DoorScenePath = "Assets/Scenes/DoorTestLevel.unity";
    static DoorProfile doorProfile;

    [MenuItem("Tools/FoxRabbit/Build Door Test Scene")]
    public static void BuildDoors()
    {
        BuildScene(DoorScenePath, BuildDoorLevel, AddDoorExtras, new Vector2(-11f, 1f), new Vector2(46.5f, 1f));
    }

    static void BuildDoorLevel()
    {
        CreateProfiles();
        doorProfile = Profile<DoorProfile>("Door_Default", p => p.openTime = 5f);

        var level = new GameObject("> Level").transform;
        var doors = new GameObject("> Doors & Consoles").transform;
        var enemies = new GameObject("> Enemies").transform;
        Color ground = new Color(0.3f, 0.36f, 0.42f);
        Color wall = new Color(0.22f, 0.26f, 0.3f);

        // Shell
        Solid("Ground", level, new Vector2(18f, -1f), new Vector2(66f, 2f), ground);
        Solid("Ceiling", level, new Vector2(18f, 8.25f), new Vector2(66f, 0.5f), wall);
        Solid("Wall_Left", level, new Vector2(-14.5f, 3.5f), new Vector2(1f, 9f), wall);
        Solid("Wall_Right", level, new Vector2(50.5f, 3.5f), new Vector2(1f, 9f), wall);
        Solid("Cover_Corridor", level, new Vector2(11f, 0.6f), new Vector2(1.2f, 1.2f), new Color(0.4f, 0.33f, 0.28f)); // also the step up to the closet

        // Doors (wall segment above each door so nobody can jump over)
        var simpleA = CreateSimpleDoor("SimpleDoor_RoomA", doors, 0f);
        CreateSimpleDoor("SimpleDoor_RoomD", doors, 41f);
        var secA = CreateSecurityDoor("SecurityDoor_A", "Door A", doors, 22f, SecurityDoor_Manager.DoorType.Toggle);
        var secB = CreateSecurityDoor("SecurityDoor_B", "Door B (timed)", doors, 34f, SecurityDoor_Manager.DoorType.Timed);
        foreach (float x in new[] { 0f, 22f, 34f, 41f })
            Solid($"Wall_Above_{x}", level, new Vector2(x, 5.5f), new Vector2(1f, 5f), wall).GetComponent<SpriteRenderer>().sortingOrder = 10;

        // Consoles: each character can only reach the console for the OTHER one's door
        CreateConsole("Console_B", "Console B", doors, -7f, new Color(1f, 0.55f, 0.15f), secB);  // in Tetra's room
        CreateConsole("Console_A", "Console A", doors, 37.5f, new Color(0.9f, 0.35f, 1f), secA); // in Pricilla's room

        // Guard in the corridor - Room A's closed door blocks its view
        CreateGuard("Guard_Corridor", enemies, new Vector2(12f, 1f), new[] { 5f, 19f }, guardVision, -5f);

        // Room C: ceiling camera sweeping the Door A entrance - Door A (closed) hides the corridor from it.
        // Kept off the exit (x 27-29) so the first character can wait there for the second one.
        CreateCamera("Camera_RoomC", enemies, new Vector2(23.5f, 7.7f), new System.Collections.Generic.List<float> { -85f, -125f }, cameraVision);
        Solid("Cover_RoomC", level, new Vector2(25f, 0.6f), new Vector2(1f, 1.2f), new Color(0.4f, 0.33f, 0.28f));


        // Corridor hiding closet (jump from the crate onto the ledge, go in, shut the door while the guard passes)
        Solid("Closet_Ledge", level, new Vector2(15.5f, 2.5f), new Vector2(6f, 0.5f), new Color(0.45f, 0.52f, 0.58f));
        Solid("Closet_WallR", level, new Vector2(18.25f, 4.25f), new Vector2(0.5f, 3f), wall);
        Solid("Closet_Roof", level, new Vector2(16.5f, 6f), new Vector2(4f, 0.5f), wall);
        Solid("Closet_AboveDoor", level, new Vector2(14.5f, 7f), new Vector2(1f, 2f), wall).GetComponent<SpriteRenderer>().sortingOrder = 10;
        CreateSimpleDoor("SimpleDoor_Closet", doors, 14.5f, 2.75f);
        Label("Sign_Closet", level, new Vector2(16.5f, 7.3f), "Hiding closet");

        // Exit in Room C (both characters)
        var goal = new GameObject("Goal");
        goal.transform.SetParent(level);
        goal.transform.position = new Vector2(28f, 1.5f);
        goal.transform.localScale = new Vector3(2f, 3f, 1f);
        var goalSr = goal.AddComponent<SpriteRenderer>();
        goalSr.sprite = square;
        goalSr.color = new Color(0.4f, 1f, 0.55f, 0.3f);
        goalSr.sortingOrder = 2;
        goal.AddComponent<BoxCollider2D>().isTrigger = true;
        var levelGoal = goal.AddComponent<LevelGoal>();
        Set(levelGoal, "requireBothCharacters", true);
        Set(levelGoal, "message", "Both made it out!");

        // Signs
        Label("Sign_A", level, new Vector2(-7f, 5.5f), "Tetra: Console B opens Pricilla's timed door.\nHold [Shift] to see the circuit.\n[Q] switch character");
        Label("Sign_Corridor", level, new Vector2(11f, 5.5f), "Closed doors block vision.\nOpen it when the guard looks away.");
        Label("Sign_C", level, new Vector2(28f, 5.5f), "EXIT - bring both");
        Label("Sign_D", level, new Vector2(44f, 5.5f), "Pricilla: Console A opens Door A.\nPortal next to a door? [E] teleports first.\nTimed door: Tetra's time stop pauses the countdown.");
    }

    static void AddDoorExtras(Context ctx)
    {
        new GameObject("GameOver_Manager").AddComponent<GameOver_Manager>();
        new GameObject("CircuitView_Manager").AddComponent<CircuitView_Manager>();
        var lens = ctx.vcam.Lens;
        lens.OrthographicSize = 6.5f;
        ctx.vcam.Lens = lens;
        ctx.camera.orthographicSize = 6.5f;
    }

    #region Builders

    static SimpleDoor_Manager CreateSimpleDoor(string name, Transform parent, float x, float y = 0f)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = new Vector2(x, y);
        var zone = root.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = new Vector2(3f, 3f);
        zone.offset = new Vector2(0f, 1.5f);

        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(root.transform, false);
        hinge.localPosition = new Vector3(-0.25f, 0f, 0f);

        var panel = new GameObject("Panel") { layer = groundLayer };
        panel.transform.SetParent(hinge, false);
        panel.transform.localPosition = new Vector3(0.25f, 1.5f, 0f);
        panel.transform.localScale = new Vector3(0.5f, 3f, 1f);
        var sr = panel.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = new Color(0.55f, 0.38f, 0.25f);
        sr.sortingOrder = 5;
        var solid = panel.AddComponent<BoxCollider2D>();
        solid.size = Vector2.one;

        // Knob so the swing is readable
        var knob = new GameObject("Knob");
        knob.transform.SetParent(panel.transform, false);
        knob.transform.localPosition = new Vector3(0.25f, 0f, 0f);
        knob.transform.localScale = new Vector3(0.3f, 0.05f, 1f);
        var knobSr = knob.AddComponent<SpriteRenderer>();
        knobSr.sprite = square;
        knobSr.color = new Color(0.95f, 0.8f, 0.3f);
        knobSr.sortingOrder = 6;

        var door = root.AddComponent<SimpleDoor_Manager>();
        Set(door, "profile", doorProfile);
        Set(door, "solidCollider", solid);
        Set(door, "hinge", hinge);
        Set(door, "popupOffset", new Vector2(0f, 3.6f));
        return door;
    }

    static SecurityDoor_Manager CreateSecurityDoor(string name, string displayName, Transform parent, float x, SecurityDoor_Manager.DoorType type)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = new Vector2(x, 0f);
        var zone = root.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = new Vector2(3f, 3f);
        zone.offset = new Vector2(0f, 1.5f);

        var panel = new GameObject("Panel") { layer = groundLayer };
        panel.transform.SetParent(root.transform, false);
        panel.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        panel.transform.localScale = new Vector3(0.7f, 3f, 1f);
        var sr = panel.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = new Color(0.5f, 0.55f, 0.62f);
        sr.sortingOrder = 5;
        var solid = panel.AddComponent<BoxCollider2D>();
        solid.size = Vector2.one;

        // Hazard stripes
        for (int i = 0; i < 3; i++)
        {
            var stripe = new GameObject($"Stripe_{i}");
            stripe.transform.SetParent(panel.transform, false);
            stripe.transform.localPosition = new Vector3(0f, -0.3f + i * 0.3f, 0f);
            stripe.transform.localScale = new Vector3(1f, 0.06f, 1f);
            var s = stripe.AddComponent<SpriteRenderer>();
            s.sprite = square;
            s.color = new Color(0.95f, 0.8f, 0.2f);
            s.sortingOrder = 6;
        }

        var light = new GameObject("StateLight");
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(-0.7f, 2.6f, 0f);
        light.transform.localScale = new Vector3(0.25f, 0.25f, 1f);
        var lightSr = light.AddComponent<SpriteRenderer>();
        lightSr.sprite = square;
        lightSr.sortingOrder = 11;

        var door = root.AddComponent<SecurityDoor_Manager>();
        Set(door, "profile", doorProfile);
        Set(door, "solidCollider", solid);
        Set(door, "panel", panel.transform);
        Set(door, "doorType", (int)type);
        Set(door, "displayName", displayName);
        Set(door, "stateLight", lightSr);
        Set(door, "popupOffset", new Vector2(0f, 3.6f));
        return door;
    }

    static SecurityConsole CreateConsole(string name, string displayName, Transform parent, float x, Color color, params SecurityDoor_Manager[] doors)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = new Vector2(x, 0f);
        var zone = root.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = new Vector2(2.2f, 2.5f);
        zone.offset = new Vector2(0f, 1.2f);

        var body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        body.transform.localScale = new Vector3(0.8f, 1.2f, 1f);
        var bodySr = body.AddComponent<SpriteRenderer>();
        bodySr.sprite = square;
        bodySr.color = new Color(0.28f, 0.3f, 0.34f);
        bodySr.sortingOrder = 4;

        var screen = new GameObject("Screen");
        screen.transform.SetParent(root.transform, false);
        screen.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        screen.transform.localScale = new Vector3(0.6f, 0.35f, 1f);
        var screenSr = screen.AddComponent<SpriteRenderer>();
        screenSr.sprite = square;
        screenSr.color = color;
        screenSr.sortingOrder = 5;

        var console = root.AddComponent<SecurityConsole>();
        Set(console, "displayName", displayName);
        Set(console, "circuitColor", color);
        Set(console, "screen", screenSr);
        Set(console, "popupOffset", new Vector2(0f, 2.6f));
        SetArray(console, "linkedDoors", doors);
        return console;
    }

    #endregion
}
