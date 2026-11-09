using UnityEngine;

/// <summary>
/// Shared vision settings for an enemy type (guard, big guard, wall camera...).
/// Create via Assets > Create > FoxRabbit > Enemy Vision Profile.
/// The direction the enemy looks is set per enemy (EnemyVision_Manager.lookAngle) and turned by EnemyMovement_Manager.
/// </summary>
[CreateAssetMenu(menuName = "FoxRabbit/Enemy Vision Profile", fileName = "EnemyVisionProfile")]
public class EnemyVisionProfile : ScriptableObject
{
    [Header("Shape")]
    [Tooltip("How far the enemy can see (radius of the vision circle).")]
    [Min(0.1f)] public float radius = 6f;
    [Tooltip("Total angle of the vision cone in degrees, like Affinity's 'Total angle' (360 = full circle).")]
    [Range(1f, 360f)] public float visionAngle = 70f;
    [Tooltip("Within this distance the enemy notices the player in any direction (e.g. bumping into them). 0 = off.")]
    [Min(0f)] public float proximityRadius = 0.6f;

    [Header("Detection")]
    [Tooltip("Seconds the player must stay in sight before getting caught. 0 = instant game over.")]
    [Min(0f)] public float timeToDetect = 0.35f;
    [Tooltip("How fast suspicion drains after losing sight (per second, 1 = full meter).")]
    [Min(0f)] public float suspicionDecay = 1.5f;
    [Tooltip("Layers that block vision (non-trigger colliders only). Players are ignored automatically.")]
    public LayerMask obstacleMask = ~0;

    [Header("Look")]
    [Tooltip("Number of rays used to draw the cone. Higher = smoother edges around obstacles.")]
    [Range(8, 256)] public int rayCount = 60;
    public Color idleColor = new Color(1f, 0.95f, 0.6f, 0.18f);
    public Color alertColor = new Color(1f, 0.25f, 0.2f, 0.35f);
}
