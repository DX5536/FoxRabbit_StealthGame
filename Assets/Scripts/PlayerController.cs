using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Reads input and drives the selected PlayerCharacter.
///   A/D or Arrows : move        Space / W / Up : jump (hold for higher)
///   Left Shift    : sprint      Q : switch character
///   F             : ability     E : interact
/// Gamepad: left stick / d-pad move, South = jump, left trigger/shoulder = sprint,
///          North = switch, West = ability, East = interact.
/// The Cinemachine camera follows the selected character.
/// </summary>
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Characters")]
    [Tooltip("Playable characters in switch order (e.g. Tetra, Pricilla).")]
    [SerializeField] List<PlayerCharacter> characters = new List<PlayerCharacter>();
    [SerializeField] int startingCharacter = 0;
    [Tooltip("Characters pass through each other instead of pushing.")]
    [SerializeField] bool ignoreCollisionBetweenCharacters = true;
    [Tooltip("Small delay between switches so spamming Q doesn't flicker.")]
    [SerializeField] float switchCooldown = 0.15f;

    [Header("Camera")]
    [Tooltip("Cinemachine camera that follows the selected character. Empty = first CinemachineCamera in the scene.")]
    [SerializeField] CinemachineCamera followCamera;

    [Header("Keyboard")]
    [SerializeField] Key leftKey = Key.A;
    [SerializeField] Key rightKey = Key.D;
    [SerializeField] Key jumpKey = Key.Space;
    [SerializeField] Key altJumpKey = Key.W;
    [SerializeField] Key sprintKey = Key.LeftShift;
    [SerializeField] Key switchKey = Key.Q;
    [SerializeField] Key abilityKey = Key.F;
    [SerializeField] Key interactKey = Key.E;
    [SerializeField] bool arrowKeysToo = true;
    [SerializeField] bool gamepadSupport = true;

    [Header("Events")]
    public UnityEvent<PlayerCharacter> onCharacterSwitched;

    /// <summary>Turn off to block player input (dialogue, pause menu, cutscenes).</summary>
    public bool InputEnabled { get; set; } = true;
    public PlayerCharacter ActiveCharacter => activeIndex >= 0 && activeIndex < characters.Count ? characters[activeIndex] : null;
    public int ActiveIndex => activeIndex;
    public IReadOnlyList<PlayerCharacter> Characters => characters;

    int activeIndex = -1;
    float lastSwitchTime = -99f;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[PlayerController] More than one PlayerController in the scene.", this);
        else Instance = this;

        characters.RemoveAll(c => c == null);
        if (characters.Count == 0)
            characters.AddRange(GetComponentsInChildren<PlayerCharacter>());

        if (followCamera == null) followCamera = FindFirstObjectByType<CinemachineCamera>();
    }

    void Start()
    {
        if (ignoreCollisionBetweenCharacters)
        {
            for (int i = 0; i < characters.Count; i++)
                for (int j = i + 1; j < characters.Count; j++)
                    IgnoreCollisions(characters[i], characters[j]);
        }

        for (int i = 0; i < characters.Count; i++)
            characters[i].SetSelected(false, true);

        if (characters.Count > 0) Select(Mathf.Clamp(startingCharacter, 0, characters.Count - 1), true);
        else Debug.LogWarning("[PlayerController] No characters assigned.", this);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        PlayerCharacter active = ActiveCharacter;
        if (active == null) return;

        if (!InputEnabled)
        {
            active.SetMoveInput(0f, false);
            active.ReleaseJump();
            return;
        }

        var kb = Keyboard.current;
        var pad = gamepadSupport ? Gamepad.current : null;

        // Move
        float move = 0f;
        if (kb != null)
        {
            if (kb[leftKey].isPressed || (arrowKeysToo && kb.leftArrowKey.isPressed)) move -= 1f;
            if (kb[rightKey].isPressed || (arrowKeysToo && kb.rightArrowKey.isPressed)) move += 1f;
        }
        if (pad != null)
        {
            float stick = pad.leftStick.x.ReadValue() + pad.dpad.x.ReadValue();
            if (Mathf.Abs(stick) > 0.2f) move = Mathf.Clamp(move + stick, -1f, 1f);
        }

        bool sprint = (kb != null && kb[sprintKey].isPressed)
                      || (pad != null && (pad.leftTrigger.isPressed || pad.leftShoulder.isPressed));
        active.SetMoveInput(move, sprint);

        // Jump
        bool jumpDown = kb != null && (kb[jumpKey].wasPressedThisFrame || kb[altJumpKey].wasPressedThisFrame
                                       || (arrowKeysToo && kb.upArrowKey.wasPressedThisFrame));
        bool jumpUp = kb != null && (kb[jumpKey].wasReleasedThisFrame || kb[altJumpKey].wasReleasedThisFrame
                                     || (arrowKeysToo && kb.upArrowKey.wasReleasedThisFrame));
        if (pad != null)
        {
            jumpDown |= pad.buttonSouth.wasPressedThisFrame;
            jumpUp |= pad.buttonSouth.wasReleasedThisFrame;
        }
        if (jumpDown) active.PressJump();
        if (jumpUp) active.ReleaseJump();

        // Switch / ability / interact
        if ((kb != null && kb[switchKey].wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame))
            SwitchCharacter();
        else if ((kb != null && kb[abilityKey].wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame))
            active.UseAbility();
        else if ((kb != null && kb[interactKey].wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            active.Interact();
    }

    #region Public API

    /// <summary>Select the next character (wraps around).</summary>
    public void SwitchCharacter()
    {
        if (characters.Count < 2 || Time.time - lastSwitchTime < switchCooldown) return;
        Select((activeIndex + 1) % characters.Count);
    }

    public void Select(int index) => Select(index, false);

    public void Select(PlayerCharacter character)
    {
        int index = characters.IndexOf(character);
        if (index >= 0) Select(index);
    }

    #endregion

    void Select(int index, bool instant)
    {
        if (index < 0 || index >= characters.Count || index == activeIndex) return;

        if (ActiveCharacter != null) ActiveCharacter.SetSelected(false, instant);
        activeIndex = index;
        lastSwitchTime = Time.time;
        ActiveCharacter.SetSelected(true, instant);

        if (followCamera != null)
        {
            followCamera.Follow = ActiveCharacter.transform;
            if (instant) followCamera.PreviousStateIsValid = false; // snap on start, glide (damping) on switch
        }

        onCharacterSwitched?.Invoke(ActiveCharacter);
    }

    static void IgnoreCollisions(PlayerCharacter a, PlayerCharacter b)
    {
        foreach (var ca in a.GetComponentsInChildren<Collider2D>())
            foreach (var cb in b.GetComponentsInChildren<Collider2D>())
                Physics2D.IgnoreCollision(ca, cb, true);
    }
}
