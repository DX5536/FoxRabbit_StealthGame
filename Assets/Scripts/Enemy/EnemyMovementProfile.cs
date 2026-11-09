using DG.Tweening;
using UnityEngine;

/// <summary>
/// Shared movement settings for an enemy type.
/// Create via Assets > Create > FoxRabbit > Enemy Movement Profile.
/// Patrol values are used by walking enemies, Rotate values by cameras.
/// </summary>
[CreateAssetMenu(menuName = "FoxRabbit/Enemy Movement Profile", fileName = "EnemyMovementProfile")]
public class EnemyMovementProfile : ScriptableObject
{
    [Header("Patrol (walking enemies)")]
    [Tooltip("Units per second.")]
    [Min(0.1f)] public float moveSpeed = 2f;
    public Ease moveEase = Ease.InOutSine;
    [Tooltip("Seconds standing still at each patrol point.")]
    [Min(0f)] public float waitDuration = 1.5f;
    [Tooltip("Seconds to turn around (vision sweeps to the new side). 0 = instant.")]
    [Min(0f)] public float turnDuration = 0.25f;

    [Header("Rotate (cameras)")]
    [Tooltip("Seconds to rotate from one angle to the next.")]
    [Min(0.05f)] public float rotateDuration = 1.2f;
    public Ease rotateEase = Ease.InOutSine;
    [Tooltip("Seconds holding each angle.")]
    [Min(0f)] public float rotateWaitDuration = 1.5f;
}
