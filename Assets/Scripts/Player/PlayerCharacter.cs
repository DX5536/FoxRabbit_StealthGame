using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// One playable character (Tetra, Pricilla...). Handles 2D movement, selection visuals and forwards
/// ability / interact input to its CharacterAbility. Driven by PlayerController.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
[DisallowMultipleComponent]
public class PlayerCharacter : MonoBehaviour
{
    [Header("Identity")]
    public string characterName = "Character";
    [Tooltip("Main body sprite (flipped when turning, tinted when not selected). Empty = SpriteRenderer on this object.")]
    [SerializeField] SpriteRenderer body;
    [Tooltip("Special ability triggered with [F]. Assign manually (e.g. TimeStop_Manager / Teleportation_Manager).")]
    public CharacterAbility ability;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float sprintMultiplier = 1.6f;
    [Tooltip("How fast the character reaches target speed on the ground (units/s²).")]
    [SerializeField] float groundAcceleration = 60f;
    [SerializeField] float groundDeceleration = 70f;
    [SerializeField] float airAcceleration = 35f;

    [Header("Jump")]
    [SerializeField] float jumpHeight = 2.2f;
    [Tooltip("Releasing jump early multiplies upward speed by this (variable jump height).")]
    [Range(0f, 1f)] [SerializeField] float jumpCutMultiplier = 0.5f;
    [Tooltip("Can still jump this long after walking off a ledge.")]
    [SerializeField] float coyoteTime = 0.1f;
    [Tooltip("Jump pressed slightly before landing still counts.")]
    [SerializeField] float jumpBufferTime = 0.12f;
    [SerializeField] float fallGravityMultiplier = 1.6f;
    [SerializeField] float maxFallSpeed = 18f;

    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayers = ~0;
    [SerializeField] float groundCheckDistance = 0.06f;

    [Header("Selection Visuals")]
    [Tooltip("Sorting order while selected (drawn in front).")]
    [SerializeField] int selectedSortingOrder = 110;
    [Tooltip("Sorting order while not selected.")]
    [SerializeField] int unselectedSortingOrder = 100;
    [Tooltip("Body tint while not selected (greyed out / blended with the background).")]
    [SerializeField] Color unselectedTint = new Color(0.55f, 0.55f, 0.6f, 0.75f);
    [SerializeField] float tintDuration = 0.2f;
    [Tooltip("Built-in squash & stretch when selected. Turn off if you use your own tween in On Selected.")]
    [SerializeField] bool selectionSquash = true;
    [SerializeField] Vector2 squashScale = new Vector2(1.2f, 0.85f);
    [SerializeField] float squashDuration = 0.25f;
    [Tooltip("Optional object (arrow / ring / outline) shown above the selected character. It bobs while active.")]
    [SerializeField] Transform selectionIndicator;
    [SerializeField] float indicatorBobHeight = 0.12f;

    [Header("Events")]
    public UnityEvent onSelected;
    public UnityEvent onDeselected;
    public UnityEvent onJump;
    public UnityEvent onLand;

    public bool IsSelected { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    public int Facing { get; private set; } = 1;
    /// <summary>Rest scale (other tweens must return here, not to a mid-animation scale).</summary>
    public Vector3 BaseScale => baseScale;
    public Rigidbody2D Body2D => rb;
    public Collider2D Collider => col;
    /// <summary>When true the character ignores movement input (e.g. while teleporting).</summary>
    public bool MovementLocked { get; set; }

    Rigidbody2D rb;
    CapsuleCollider2D col;
    SortingGroup sortingGroup;
    Color bodyBaseColor = Color.white;
    Vector3 baseScale;
    Vector3 indicatorBasePos;

    float moveInput;
    bool jumpHeld;
    float lastGroundedTime = -99f;
    float lastJumpPressedTime = -99f;
    bool jumpCutPending;
    bool isJumping;

    readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>();
    ContactFilter2D groundFilter;

    Tween tintTween, squashTween, indicatorTween;

    #region Unity

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (body != null) bodyBaseColor = body.color;
        baseScale = transform.localScale;

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // A SortingGroup keeps the character and its children (hourglass, portal bars) together when the order changes.
        sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup == null) sortingGroup = gameObject.AddComponent<SortingGroup>();
        if (body != null) sortingGroup.sortingLayerID = body.sortingLayerID;

        groundFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = groundLayers };

        if (selectionIndicator != null)
        {
            indicatorBasePos = selectionIndicator.localPosition;
            selectionIndicator.gameObject.SetActive(false);
        }

        if (ability != null) ability.Bind(this);
        ApplySelectionInstant(false);
    }

    void Update()
    {
        UpdateGrounded();
    }

    void FixedUpdate()
    {
        Vector2 v = rb.linearVelocity;

        float input = IsSelected && !MovementLocked ? moveInput : 0f;
        float target = input * moveSpeed * (IsSprinting && IsSelected ? sprintMultiplier : 1f);
        float accel = IsGrounded
            ? (Mathf.Abs(target) > 0.01f ? groundAcceleration : groundDeceleration)
            : airAcceleration;
        v.x = Mathf.MoveTowards(v.x, target, accel * Time.fixedDeltaTime);

        // Jump (buffer + coyote time)
        bool canJump = IsSelected && !MovementLocked && Time.time - lastGroundedTime <= coyoteTime && !isJumping;
        if (canJump && Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
            v.y = Mathf.Sqrt(2f * gravity * jumpHeight);
            isJumping = true;
            lastJumpPressedTime = -99f;
            lastGroundedTime = -99f;
            onJump?.Invoke();
        }

        // Variable jump height
        if (jumpCutPending)
        {
            if (v.y > 0f) v.y *= jumpCutMultiplier;
            jumpCutPending = false;
        }

        // Snappier fall
        if (v.y < 0f)
            v.y += Physics2D.gravity.y * rb.gravityScale * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        v.y = Mathf.Max(v.y, -maxFallSpeed);

        rb.linearVelocity = v;
    }

    void OnDestroy()
    {
        tintTween?.Kill();
        squashTween?.Kill();
        indicatorTween?.Kill();
    }

    #endregion

    #region Input (called by PlayerController)

    public void SetMoveInput(float horizontal, bool sprint)
    {
        moveInput = Mathf.Clamp(horizontal, -1f, 1f);
        IsSprinting = sprint && Mathf.Abs(moveInput) > 0.01f;

        if (IsSelected && !MovementLocked && Mathf.Abs(moveInput) > 0.01f)
        {
            int newFacing = moveInput > 0 ? 1 : -1;
            if (newFacing != Facing)
            {
                Facing = newFacing;
                if (body != null) body.flipX = Facing < 0;
            }
        }
    }

    public void PressJump()
    {
        lastJumpPressedTime = Time.time;
        jumpHeld = true;
    }

    public void ReleaseJump()
    {
        if (jumpHeld && isJumping) jumpCutPending = true;
        jumpHeld = false;
    }

    public void UseAbility()
    {
        if (ability != null) ability.Trigger();
    }

    /// <summary>
    /// [E]: the ability gets first say (e.g. Pricilla standing in a portal teleports instead of opening a door),
    /// otherwise the closest Interactable in range is used.
    /// </summary>
    public bool Interact()
    {
        if (ability != null && ability.Interact()) return true;
        Interactable target = ClosestInteractable();
        return target != null && target.Interact(this);
    }

    readonly List<Interactable> nearbyInteractables = new List<Interactable>();
    public IReadOnlyList<Interactable> NearbyInteractables => nearbyInteractables;

    public void AddInteractable(Interactable interactable)
    {
        if (!nearbyInteractables.Contains(interactable)) nearbyInteractables.Add(interactable);
    }

    public void RemoveInteractable(Interactable interactable) => nearbyInteractables.Remove(interactable);

    /// <summary>Closest interactable this character can use right now (highest priority wins ties).</summary>
    public Interactable ClosestInteractable()
    {
        Interactable best = null;
        float bestScore = float.MaxValue;
        Vector2 pos = col.bounds.center;
        for (int i = nearbyInteractables.Count - 1; i >= 0; i--)
        {
            var it = nearbyInteractables[i];
            if (it == null || !it.isActiveAndEnabled) { nearbyInteractables.RemoveAt(i); continue; }
            if (!it.CanInteract(this)) continue;
            float score = Vector2.Distance(pos, it.InteractPoint) - it.Priority * 100f;
            if (score < bestScore) { bestScore = score; best = it; }
        }
        return best;
    }

    #endregion

    #region Selection

    public void SetSelected(bool selected, bool instant = false)
    {
        if (IsSelected == selected && !instant) return;
        IsSelected = selected;

        if (!selected)
        {
            moveInput = 0f;
            IsSprinting = false;
            jumpHeld = false;
        }

        if (ability != null) ability.SetSelected(selected);

        if (instant) ApplySelectionInstant(selected);
        else ApplySelectionAnimated(selected);

        if (selected) onSelected?.Invoke();
        else onDeselected?.Invoke();
    }

    void ApplySelectionInstant(bool selected)
    {
        sortingGroup.sortingOrder = selected ? selectedSortingOrder : unselectedSortingOrder;
        if (body != null) body.color = selected ? bodyBaseColor : bodyBaseColor * unselectedTint;
        if (selectionIndicator != null) selectionIndicator.gameObject.SetActive(selected);
    }

    void ApplySelectionAnimated(bool selected)
    {
        sortingGroup.sortingOrder = selected ? selectedSortingOrder : unselectedSortingOrder;

        tintTween?.Kill();
        if (body != null)
            tintTween = body.DOColor(selected ? bodyBaseColor : bodyBaseColor * unselectedTint, tintDuration).SetLink(gameObject);

        if (selected && selectionSquash)
        {
            squashTween?.Kill();
            transform.localScale = baseScale;
            squashTween = DOTween.Sequence()
                .Append(transform.DOScale(new Vector3(baseScale.x * squashScale.x, baseScale.y * squashScale.y, baseScale.z), squashDuration * 0.35f).SetEase(Ease.OutQuad))
                .Append(transform.DOScale(new Vector3(baseScale.x * squashScale.y, baseScale.y * squashScale.x, baseScale.z), squashDuration * 0.3f).SetEase(Ease.InOutQuad))
                .Append(transform.DOScale(baseScale, squashDuration * 0.35f).SetEase(Ease.OutBack))
                .SetLink(gameObject);
        }

        if (selectionIndicator != null)
        {
            indicatorTween?.Kill();
            selectionIndicator.gameObject.SetActive(selected);
            if (selected)
            {
                selectionIndicator.localPosition = indicatorBasePos;
                selectionIndicator.localScale = Vector3.zero;
                indicatorTween = DOTween.Sequence()
                    .Append(selectionIndicator.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
                    .Append(selectionIndicator.DOLocalMoveY(indicatorBasePos.y + indicatorBobHeight, 0.5f)
                        .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo))
                    .SetLink(gameObject);
            }
        }
    }

    #endregion

    #region Helpers

    void UpdateGrounded()
    {
        bool wasGrounded = IsGrounded;
        groundFilter.layerMask = groundLayers;
        groundHits.Clear();
        int count = col.Cast(Vector2.down, groundFilter, groundHits, groundCheckDistance, true);

        IsGrounded = false;
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].collider.isTrigger) continue;
            var otherRb = groundHits[i].collider.attachedRigidbody;
            if (otherRb != null && otherRb != rb && otherRb.GetComponent<PlayerCharacter>() != null) continue; // don't stand on the other character
            if (groundHits[i].normal.y > 0.5f) { IsGrounded = true; break; }
        }

        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            if (rb.linearVelocity.y <= 0.01f) isJumping = false;
            if (!wasGrounded) onLand?.Invoke();
        }
    }

    /// <summary>Instantly move the character (used by teleport). Clears velocity.</summary>
    public void TeleportTo(Vector2 position)
    {
        rb.position = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        rb.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    /// <summary>World position of the character's feet (bottom of the collider).</summary>
    public Vector2 FeetPosition
    {
        get
        {
            Bounds b = col.bounds;
            return new Vector2(b.center.x, b.min.y);
        }
    }

    #endregion
}
