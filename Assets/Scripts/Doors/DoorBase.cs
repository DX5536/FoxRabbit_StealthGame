using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Shared door logic: open/closed state, a solid collider that blocks movement and enemy vision while closed,
/// and a doorway check so the door never closes on a character.
/// Put the solid collider on the obstacle layer used by EnemyVisionProfile.obstacleMask (e.g. Ground).
/// </summary>
public abstract class DoorBase : Interactable
{
    [Header("Door")]
    [SerializeField] protected DoorProfile profile;
    [Tooltip("Collider that blocks characters and enemy vision while closed (non-trigger, usually on the panel).")]
    [SerializeField] protected Collider2D solidCollider;
    [SerializeField] protected bool startOpen = false;

    [Header("Door Events")]
    public UnityEvent onOpen;
    public UnityEvent onClose;
    [Tooltip("Tried to close but a character is in the doorway.")]
    public UnityEvent onBlocked;

    public bool IsOpen { get; private set; }
    public DoorProfile Profile => profile;
    public override Vector2 InteractPoint => solidCollider != null ? (Vector2)solidCollider.bounds.center : (Vector2)transform.position;

    static readonly Collider2D[] overlapBuffer = new Collider2D[16];
    Bounds closedBounds;

    protected override void Awake()
    {
        base.Awake();
        if (profile == null) profile = ScriptableObject.CreateInstance<DoorProfile>();
        if (solidCollider != null) closedBounds = solidCollider.bounds;
    }

    protected virtual void Start()
    {
        IsOpen = startOpen;
        ApplyState(startOpen, true);
    }

    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);
    public void Toggle() => SetOpen(!IsOpen);

    /// <summary>Returns false if closing was refused because the doorway is occupied.</summary>
    public bool SetOpen(bool open)
    {
        if (open == IsOpen) return true;
        if (!open && profile.preventClosingOnCharacters && DoorwayOccupied())
        {
            onBlocked?.Invoke();
            return false;
        }

        IsOpen = open;
        ApplyState(open, false);
        if (open) onOpen?.Invoke();
        else onClose?.Invoke();
        PunchPopup();
        return true;
    }

    /// <summary>Animate (or snap, if instant) the door to the given state.</summary>
    protected abstract void ApplyState(bool open, bool instant);

    /// <summary>Is a character standing where the closed door would be?</summary>
    public bool DoorwayOccupied()
    {
        if (solidCollider == null) return false;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.NoFilter();
        filter.useTriggers = false;
        int n = Physics2D.OverlapBox(closedBounds.center, closedBounds.size * 0.95f, 0f, filter, overlapBuffer);
        for (int i = 0; i < n; i++)
        {
            var rb = overlapBuffer[i].attachedRigidbody;
            if (rb != null && rb.GetComponent<PlayerCharacter>() != null) return true;
        }
        return false;
    }

    protected string StateText => IsOpen ? Colored("OPEN", new Color(0.45f, 1f, 0.55f)) : Colored("CLOSED", new Color(1f, 0.45f, 0.4f));
}
