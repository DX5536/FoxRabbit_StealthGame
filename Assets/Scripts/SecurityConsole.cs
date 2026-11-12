using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Console that operates SecurityDoor_Manager doors with [E] (any character).
/// Link doors by dragging them into Linked Doors, and/or by giving console and doors the same Channel Id.
/// Circuit Color is used for the [Shift] circuit lines (CircuitView_Manager) and the console's screen.
/// </summary>
public class SecurityConsole : Interactable
{
    /// <summary>Every console in the loaded scenes.</summary>
    public static readonly List<SecurityConsole> All = new List<SecurityConsole>();

    [Header("Console")]
    [SerializeField] string displayName = "Console";
    [SerializeField] List<SecurityDoor_Manager> linkedDoors = new List<SecurityDoor_Manager>();
    [Tooltip("Also controls every SecurityDoor with this Channel Id.")]
    [SerializeField] string channelId = "";
    [Tooltip("Color of the circuit lines to its doors (and the screen tint).")]
    [SerializeField] Color circuitColor = new Color(0.3f, 0.9f, 1f);
    [Tooltip("Optional screen/light sprite tinted with the circuit color.")]
    [SerializeField] SpriteRenderer screen;
    [SerializeField] float useCooldown = 0.25f;

    public Color CircuitColor { get => circuitColor; set { circuitColor = value; TintScreen(); } }
    public string DisplayName => displayName;

    readonly List<SecurityDoor_Manager> doorBuffer = new List<SecurityDoor_Manager>();
    float lastUse = -99f;

    void OnEnable() => All.Add(this);

    protected override void OnDisable()
    {
        base.OnDisable();
        All.Remove(this);
    }

    void Start() => TintScreen();

    /// <summary>Doors linked directly or through the channel id.</summary>
    public List<SecurityDoor_Manager> GetDoors()
    {
        doorBuffer.Clear();
        foreach (var d in linkedDoors) if (d != null && !doorBuffer.Contains(d)) doorBuffer.Add(d);
        if (!string.IsNullOrEmpty(channelId))
            foreach (var d in SecurityDoor_Manager.All)
                if (d.IsControlledBy(channelId) && !doorBuffer.Contains(d)) doorBuffer.Add(d);
        return doorBuffer;
    }

    public override bool CanInteract(PlayerCharacter character) => base.CanInteract(character) && GetDoors().Count > 0;

    protected override bool OnInteract(PlayerCharacter character)
    {
        if (Time.time - lastUse < useCooldown) return false;
        lastUse = Time.time;

        foreach (var door in GetDoors()) door.ActivateFromConsole();

        if (screen != null)
        {
            screen.DOKill();
            screen.color = Color.white;
            screen.DOColor(circuitColor, 0.3f).SetLink(gameObject);
        }
        return true;
    }

    protected override string GetStatusText(PlayerCharacter character)
    {
        var sb = new StringBuilder();
        sb.Append(Colored(displayName, circuitColor));
        bool anyTimed = false;
        foreach (var door in GetDoors())
        {
            anyTimed |= door.Type == SecurityDoor_Manager.DoorType.Timed;
            sb.Append("\n<size=80%>").Append(door.DisplayName).Append(": ")
              .Append(door.IsOpen ? Colored("OPEN", new Color(0.45f, 1f, 0.55f)) : Colored("CLOSED", new Color(1f, 0.45f, 0.4f)));
            if (door.Type == SecurityDoor_Manager.DoorType.Timed && door.IsOpen) sb.Append($" {door.TimeLeft:0.0}s");
            sb.Append("</size>");
        }
        sb.Append($"\n<size=70%>[{Keybinds.Label(GameAction.Interact)}] ").Append(anyTimed ? "Open" : "Toggle")
          .Append($"   [{Keybinds.Label(GameAction.ShowCircuit)}] show circuit</size>");
        return sb.ToString();
    }

    void TintScreen()
    {
        if (screen != null) screen.color = circuitColor;
    }
}
