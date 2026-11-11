using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pricilla's ability.
///   [F] places portal_0, then portal_1. After that each press moves the OLDEST portal to her position
///       (0, 1, 0, 1 ...). No cooldown - she is only limited by her two portals.
///   [F] while standing in one of her portals picks it back up (bar refills) so she can place it elsewhere.
///   [E] while standing in a portal (and both exist) teleports her to the other portal.
/// The two charge bars next to her show which portal is placed and which one goes down next.
/// Assign this component to Pricilla's PlayerCharacter.ability.
/// </summary>
public class Teleportation_Manager : CharacterAbility
{
    [Header("Portals")]
    [Tooltip("Portal prefabs. Element 0 = portal_0, element 1 = portal_1. If only one is set it is used for both.")]
    [SerializeField] GameObject[] portalPrefabs = new GameObject[2];
    [Tooltip("Offset from Pricilla's feet where the portal is placed.")]
    [SerializeField] Vector2 placeOffset = new Vector2(0f, 0.5f);
    [Tooltip("Only allow placing portals while standing on the ground.")]
    [SerializeField] bool requireGrounded = false;
    [Tooltip("Optional parent for spawned portals (keeps the hierarchy clean). Empty = scene root.")]
    [SerializeField] Transform portalParent;

    [Header("Teleport")]
    [Tooltip("Time between pressing E and arriving (room for a vanish animation). 0 = instant.")]
    [SerializeField] float teleportDelay = 0.15f;
    [Tooltip("Built-in shrink/grow effect on Pricilla while teleporting.")]
    [SerializeField] bool builtInTeleportEffect = true;
    [Tooltip("Ignore E for this long after arriving so she doesn't bounce straight back.")]
    [SerializeField] float teleportCooldown = 0.25f;

    [Header("Charge Bars (2 bars next to Pricilla)")]
    [Tooltip("Element 0 = bar for portal_0, element 1 = bar for portal_1.")]
    [SerializeField] SpriteRenderer[] chargeBars = new SpriteRenderer[2];
    [SerializeField] Color barAvailableColor = Color.white;
    [SerializeField] Color barUsedColor = new Color(1f, 1f, 1f, 0.25f);
    [Tooltip("When both portals are down, the bar of the portal that moves next pulses.")]
    [SerializeField] bool pulseNextBar = true;

    [Header("Teleport Events")]
    public UnityEvent<int> onPortalPlaced;      // index placed (0 or 1)
    public UnityEvent<int> onPortalMoved;       // index moved to a new location (oldest one)
    public UnityEvent<int> onPortalRecalled;    // index picked back up with [F] while standing in it
    public UnityEvent onTeleportStart;
    public UnityEvent onTeleported;
    [Tooltip("Pressed E inside a portal but the other portal does not exist yet.")]
    public UnityEvent onTeleportFailed;

    public bool IsTeleporting { get; private set; }
    public Portal CurrentPortal { get; private set; }
    public int NextPortalIndex => nextIndex;
    /// <summary>The portal at this index, or null if it is not placed.</summary>
    public Portal GetPortal(int index) => IsPlaced(index) ? portals[index] : null;
    public bool IsPlaced(int index) => portals[index] != null && portals[index].gameObject.activeSelf;

    readonly Portal[] portals = new Portal[2];
    readonly Tween[] barTweens = new Tween[2];
    readonly Vector3[] barBaseScales = { Vector3.one, Vector3.one };
    int nextIndex;
    Portal recallLock;
    float lastTeleportTime = -99f;
    Sequence teleportSequence;

    void Start()
    {
        for (int i = 0; i < 2 && chargeBars != null && i < chargeBars.Length; i++)
            if (chargeBars[i] != null) barBaseScales[i] = chargeBars[i].transform.localScale;
        RefreshBars(true);
    }

    void OnDestroy()
    {
        teleportSequence?.Kill();
        foreach (var t in barTweens) t?.Kill();
    }

    public override bool CanActivate()
    {
        if (!base.CanActivate() || Character == null || IsTeleporting) return false;
        if (requireGrounded && !Character.IsGrounded) return false;
        return true;
    }

    protected override bool Activate()
    {
        // Standing in one of her portals: pick it back up (not the one she just placed under her feet).
        if (CurrentPortal != null && CurrentPortal != recallLock && IsPlaced(CurrentPortal.Index))
        {
            Recall(CurrentPortal.Index);
            return true;
        }

        int index = nextIndex;
        Vector2 pos = Character.FeetPosition + placeOffset;

        bool moved = IsPlaced(index);
        if (portals[index] == null)
        {
            GameObject prefab = GetPrefab(index);
            if (prefab == null)
            {
                Debug.LogWarning("[Teleportation_Manager] No portal prefab assigned.", this);
                return false;
            }

            GameObject go = Instantiate(prefab, pos, Quaternion.identity, portalParent);
            go.name = $"Portal_{index}";
            var portal = go.GetComponent<Portal>();
            if (portal == null) portal = go.AddComponent<Portal>();
            portal.Init(this, index);
            portals[index] = portal;
        }
        else
        {
            if (CurrentPortal == portals[index]) NotifyExit(portals[index]);
            portals[index].transform.position = pos;
            portals[index].gameObject.SetActive(true); // reuse a recalled portal
        }

        portals[index].OnPlaced();
        recallLock = portals[index]; // can't be picked up until she has stepped out of it once
        nextIndex = 1 - index; // alternate 0, 1, 0, 1...

        if (moved) onPortalMoved?.Invoke(index);
        else onPortalPlaced?.Invoke(index);

        RefreshBars(false);
        return true;
    }

    /// <summary>Remove a placed portal and give the charge back. The next [F] places this portal again.</summary>
    public void Recall(int index)
    {
        if (!IsPlaced(index)) return;
        Portal portal = portals[index];
        if (CurrentPortal == portal) NotifyExit(portal);
        portal.SetPrompts(false, false);
        portal.gameObject.SetActive(false);
        nextIndex = index;
        onPortalRecalled?.Invoke(index);
        UpdatePrompts();
        RefreshBars(false);
    }

    public override bool Interact()
    {
        if (Character == null || IsTeleporting || CurrentPortal == null) return false;
        if (Time.time - lastTeleportTime < teleportCooldown) return false;

        Portal from = CurrentPortal;
        Portal to = GetPortal(1 - from.Index);
        if (to == null)
        {
            onTeleportFailed?.Invoke();
            return false;
        }

        StartTeleport(from, to);
        return true;
    }

    void StartTeleport(Portal from, Portal to)
    {
        IsTeleporting = true;
        Character.MovementLocked = true;
        onTeleportStart?.Invoke();
        from.onTeleportFrom?.Invoke();

        Transform t = Character.transform;
        Vector3 scale = Character.BaseScale; // not t.localScale: a selection squash may be mid-animation
        float half = Mathf.Max(teleportDelay, 0f) * 0.5f;

        teleportSequence?.Kill();
        teleportSequence = DOTween.Sequence().SetLink(Character.gameObject);
        if (builtInTeleportEffect && half > 0f)
            teleportSequence.Append(t.DOScale(new Vector3(scale.x * 0.2f, scale.y * 1.4f, scale.z), half).SetEase(Ease.InBack));
        else if (half > 0f)
            teleportSequence.AppendInterval(half);

        teleportSequence.AppendCallback(() =>
        {
            Vector2 feetToPivot = (Vector2)t.position - Character.FeetPosition;
            Character.TeleportTo(to.ExitPoint - placeOffset + feetToPivot);
            to.onTeleportTo?.Invoke();
        });

        if (builtInTeleportEffect && half > 0f)
            teleportSequence.Append(t.DOScale(scale, half).SetEase(Ease.OutBack));
        else if (half > 0f)
            teleportSequence.AppendInterval(half);

        teleportSequence.OnComplete(() => FinishTeleport(scale));
        teleportSequence.OnKill(() => { if (IsTeleporting) FinishTeleport(scale); });
    }

    void FinishTeleport(Vector3 scale)
    {
        if (Character != null)
        {
            Character.transform.localScale = scale;
            Character.MovementLocked = false;
        }
        IsTeleporting = false;
        lastTeleportTime = Time.time;
        onTeleported?.Invoke();
    }

    #region Called by Portal

    public void NotifyEnter(Portal portal)
    {
        CurrentPortal = portal;
        UpdatePrompts();
    }

    public void NotifyExit(Portal portal)
    {
        if (CurrentPortal == portal) CurrentPortal = null;
        if (recallLock == portal && portal.gameObject.activeSelf) recallLock = null;
        portal.SetPrompts(false, false);
        UpdatePrompts();
    }

    #endregion

    protected override void OnSelectedChanged(bool selected) => UpdatePrompts();

    void UpdatePrompts()
    {
        for (int i = 0; i < 2; i++)
        {
            if (!IsPlaced(i)) continue;
            bool inside = IsSelected && portals[i] == CurrentPortal;
            portals[i].SetPrompts(inside && IsPlaced(1 - i), inside && portals[i] != recallLock);
        }
    }

    GameObject GetPrefab(int index)
    {
        if (portalPrefabs == null || portalPrefabs.Length == 0) return null;
        if (index < portalPrefabs.Length && portalPrefabs[index] != null) return portalPrefabs[index];
        foreach (var p in portalPrefabs) if (p != null) return p;
        return null;
    }

    void RefreshBars(bool instant)
    {
        if (chargeBars == null) return;
        bool bothPlaced = IsPlaced(0) && IsPlaced(1);

        for (int i = 0; i < chargeBars.Length && i < 2; i++)
        {
            SpriteRenderer bar = chargeBars[i];
            if (bar == null) continue;

            barTweens[i]?.Kill();
            bar.transform.localScale = barBaseScales[i];
            Color target = IsPlaced(i) ? barUsedColor : barAvailableColor;

            if (instant)
            {
                bar.color = target;
                continue;
            }

            if (bothPlaced && pulseNextBar && i == nextIndex)
            {
                // Next portal to be moved: gently pulse between used and available.
                bar.color = barUsedColor;
                barTweens[i] = bar.DOColor(Color.Lerp(barUsedColor, barAvailableColor, 0.7f), 0.6f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(bar.gameObject);
            }
            else
            {
                barTweens[i] = DOTween.Sequence()
                    .Append(bar.DOColor(target, 0.15f))
                    .Join(bar.transform.DOPunchScale(barBaseScales[i] * 0.25f, 0.25f, 6, 0.5f))
                    .SetLink(bar.gameObject);
            }
        }
    }
}
