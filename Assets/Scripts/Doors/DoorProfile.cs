using DG.Tweening;
using UnityEngine;

/// <summary>
/// Shared door settings. Create via Assets > Create > FoxRabbit > Door Profile.
/// Swing values are used by SimpleDoor_Manager, Slide/Timer values by SecurityDoor_Manager.
/// </summary>
[CreateAssetMenu(menuName = "FoxRabbit/Door Profile", fileName = "DoorProfile")]
public class DoorProfile : ScriptableObject
{
    [Header("Swing (Simple Door)")]
    [Tooltip("How far the door swings open around its hinge (Y axis), in degrees.")]
    [Range(30f, 89f)] public float openAngle = 80f;
    [Min(0.01f)] public float swingDuration = 0.3f;
    public Ease openEase = Ease.OutBack;
    public Ease closeEase = Ease.InQuad;

    [Header("Slide (Security Door)")]
    [Tooltip("Seconds to slide fully up or down.")]
    [Min(0.01f)] public float slideDuration = 0.45f;
    public Ease slideOpenEase = Ease.OutQuad;
    public Ease slideCloseEase = Ease.InQuad;
    [Tooltip("Timed doors: seconds the door stays open before closing.")]
    [Min(0.1f)] public float openTime = 5f;
    [Tooltip("Timed doors: the countdown turns red/pulses below this many seconds.")]
    [Min(0f)] public float warningTime = 1.5f;

    [Header("Safety")]
    [Tooltip("Don't close while a character stands in the doorway (waits until it is clear).")]
    public bool preventClosingOnCharacters = true;
}
