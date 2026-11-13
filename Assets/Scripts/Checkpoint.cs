using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Touch it with either character: after a restart ([R]) both characters respawn here.
/// Guards, doors and timers reset by themselves because restart reloads the scene.
/// Reaching the level goal clears the checkpoint (next restart starts from the beginning).
/// Create the prefab with Tools > FoxRabbit > Create Checkpoint Prefab, then drag it into a level.
/// Place it where no enemy can see the respawn spots (e.g. behind a closed door): after a restart both
/// characters stand there, and the guards are back at their start positions.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Optional exact respawn spots, in PlayerController character order (e.g. Tetra, Pricilla).\nEmpty = both appear here side by side.")]
    [SerializeField] Transform[] spawnPoints;
    [Tooltip("Gap between the characters when no spawn points are set.")]
    [SerializeField] float spacing = 1.2f;
    [Tooltip("Optional sprite tinted when this checkpoint is active.")]
    [SerializeField] SpriteRenderer flag;
    [SerializeField] Color inactiveColor = new Color(0.6f, 0.6f, 0.65f);
    [SerializeField] Color activeColor = new Color(0.4f, 1f, 0.55f);
    public UnityEvent onActivated;

    // Survives the scene reload; keyed by scene so a checkpoint never leaks into another level.
    static string savedScene;
    static Vector2 savedAnchor;
    static Vector2[] savedSpawns;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear() => savedScene = null;

    /// <summary>Respawn positions for this scene, in character order, if a checkpoint was reached.</summary>
    public static bool TryGetSpawns(out Vector2[] spawns)
    {
        spawns = savedSpawns;
        return savedScene == SceneManager.GetActiveScene().path && spawns != null;
    }

    bool IsActive => savedScene == gameObject.scene.path && (savedAnchor - (Vector2)transform.position).sqrMagnitude < 0.01f;

    void Reset() => GetComponent<Collider2D>().isTrigger = true;

    void Start() => Tint();

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsActive || PlayerCharacter.From(other) == null || PlayerController.Instance == null) return;

        var characters = PlayerController.Instance.Characters;
        var spawns = new Vector2[characters.Count];
        for (int i = 0; i < spawns.Length; i++)
            spawns[i] = spawnPoints != null && i < spawnPoints.Length && spawnPoints[i] != null
                ? (Vector2)spawnPoints[i].position
                : (Vector2)transform.position + Vector2.right * (i - (spawns.Length - 1) * 0.5f) * spacing;

        savedScene = gameObject.scene.path;
        savedAnchor = transform.position;
        savedSpawns = spawns;

        foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None)) cp.Tint();
        onActivated?.Invoke();
    }

    void Tint()
    {
        if (flag != null) flag.color = IsActive ? activeColor : inactiveColor;
    }
}
