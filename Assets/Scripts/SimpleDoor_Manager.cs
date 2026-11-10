using DG.Tweening;
using UnityEngine;

/// <summary>
/// A normal swinging door. Stand next to it and press [E] to open / close.
/// The panel swings around its hinge (Y rotation, like a real door seen from the side).
/// While closed its solid collider blocks characters and enemy vision; while open the collider is off.
///
/// Setup: root (this script + trigger zone) > Hinge (at the door edge, rotated) > Panel (sprite + solid BoxCollider2D).
/// Pricilla: if she stands in one of her portals, [E] teleports first (handled by PlayerCharacter).
/// </summary>
public class SimpleDoor_Manager : DoorBase
{
    [Header("Swing")]
    [Tooltip("Transform rotated when opening (put it at the door's hinge edge). Empty = this transform.")]
    [SerializeField] Transform hinge;
    [Tooltip("Flip to swing the other way.")]
    [SerializeField] bool invertSwing = false;

    Tween swingTween;

    protected override void Awake()
    {
        if (hinge == null) hinge = transform;
        base.Awake();
    }

    protected override bool OnInteract(PlayerCharacter character) => SetOpen(!IsOpen);

    protected override void ApplyState(bool open, bool instant)
    {
        float angle = open ? profile.openAngle * (invertSwing ? -1f : 1f) : 0f;
        Vector3 target = new Vector3(0f, angle, 0f);

        // Closed door must block immediately; open door stops blocking right away (feels responsive).
        if (solidCollider != null) solidCollider.enabled = !open;

        swingTween?.Kill();
        if (instant) hinge.localEulerAngles = target;
        else swingTween = hinge.DOLocalRotate(target, profile.swingDuration)
            .SetEase(open ? profile.openEase : profile.closeEase).SetLink(gameObject);
    }

    protected override string GetStatusText(PlayerCharacter character)
        => $"Door: {StateText}\n<size=75%>[E] {(IsOpen ? "Close" : "Open")}</size>";

    void OnDestroy() => swingTween?.Kill();
}
