using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A placed portal. Added automatically to the portal prefab instance by Teleportation_Manager if missing.
/// Only the owner's character is detected, so only Pricilla can use her portals.
/// </summary>
[DisallowMultipleComponent]
public class Portal : MonoBehaviour
{
    [Tooltip("Optional [E] teleport icon. Shown while the owner stands in this portal and the other portal exists.")]
    public GameObject teleportPrompt;
    [Tooltip("Optional [F] pick-up icon. Shown while the owner stands in this portal.")]
    public GameObject recallPrompt;
    [Tooltip("Where the character appears when arriving at this portal, relative to the portal.")]
    public Vector2 exitOffset = Vector2.zero;

    [Header("Events")]
    public UnityEvent onOwnerEnter;
    public UnityEvent onOwnerExit;
    public UnityEvent onPlaced;
    public UnityEvent onTeleportFrom;
    public UnityEvent onTeleportTo;

    public int Index { get; private set; }
    public Teleportation_Manager Owner { get; private set; }
    public bool OwnerInside { get; private set; }
    public Vector2 ExitPoint => (Vector2)transform.position + exitOffset;

    public void Init(Teleportation_Manager owner, int index)
    {
        Owner = owner;
        Index = index;
        EnsureTrigger();
        SetPrompts(false, false);
    }

    void EnsureTrigger()
    {
        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                box.size = sr.sprite.bounds.size;
                box.offset = sr.sprite.bounds.center;
            }
            col = box;
        }
        col.isTrigger = true;
    }

    /// <summary>Called when the portal is (re)placed somewhere new.</summary>
    public void OnPlaced()
    {
        OwnerInside = false;
        SetPrompts(false, false);
        onPlaced?.Invoke();
        Physics2D.SyncTransforms();
    }

    public void SetPrompts(bool showTeleport, bool showRecall)
    {
        if (teleportPrompt != null) teleportPrompt.SetActive(showTeleport);
        if (recallPrompt != null) recallPrompt.SetActive(showRecall);
    }

    void OnDisable()
    {
        // Disabled portals get no exit callback, so reset here (recalled portals are disabled).
        OwnerInside = false;
    }

    bool IsOwner(Collider2D other)
    {
        return Owner != null && Owner.Character != null && PlayerCharacter.From(other) == Owner.Character;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsOwner(other) || OwnerInside) return;
        OwnerInside = true;
        Owner.NotifyEnter(this);
        onOwnerEnter?.Invoke();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsOwner(other) || !OwnerInside) return;
        OwnerInside = false;
        Owner.NotifyExit(this);
        onOwnerExit?.Invoke();
    }
}
