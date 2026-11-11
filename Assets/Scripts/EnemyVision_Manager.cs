using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Vision of one enemy (guard, camera...).
///  - The "eye" is any Transform (empty GameObject) you can place anywhere: head of a big enemy, lens of a wall camera.
///  - Shape comes from an EnemyVisionProfile (radius + vision angle). Direction is LookAngle (0 = right, 90 = up,
///    180 = left, -90 = down), turned by EnemyMovement_Manager.
///  - Non-trigger colliders on the obstacle layers block sight; the drawn cone stops at them.
///  - Seeing a player fills a suspicion meter; when full the player is caught -> GameOver_Manager.
///  - Tetra's time stop blinds enemies; Pricilla inside her portal / teleporting is not seen (both toggleable).
/// </summary>
[DisallowMultipleComponent]
public class EnemyVision_Manager : MonoBehaviour
{
    [SerializeField] EnemyVisionProfile profile;
    [Tooltip("Vision origin. Empty = this transform.")]
    [SerializeField] Transform eye;
    [Tooltip("World direction the enemy looks at in degrees: 0 = right, 90 = up, 180 = left, -90 = down.")]
    [SerializeField] float lookAngle = 0f;

    [Header("Display")]
    [Tooltip("Draw the vision cone in game (players need to see it in a stealth game).")]
    [SerializeField] bool showVisionCone = true;
    [Tooltip("Optional material for the cone. Empty = URP Sprite-Unlit-Default.")]
    [SerializeField] Material coneMaterial;
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] int sortingOrder = 40;
    [Tooltip("Optional object shown while the enemy sees a player (e.g. a '!' sprite).")]
    [SerializeField] GameObject alertIcon;

    [Header("Debug")]
    [Tooltip("Show the full vision circle, every ray and the line to a seen player (Game view + Scene view).")]
    [SerializeField] bool debugView = false;
    [SerializeField] Color debugCircleColor = new Color(0.3f, 1f, 1f, 0.6f);

    [Header("Rules")]
    [Tooltip("Enemies see nothing during Tetra's time stop.")]
    [SerializeField] bool blindDuringTimeStop = true;
    [Tooltip("Pricilla is not seen while standing in one of her portals or teleporting.")]
    [SerializeField] bool ignorePricillaInPortal = true;
    [Tooltip("The character you are not controlling can also be spotted (hide both!).")]
    [SerializeField] bool detectUnselectedCharacter = true;
    [Tooltip("Call GameOver_Manager when the player is caught.")]
    [SerializeField] bool triggerGameOver = true;

    [Header("Events")]
    public UnityEvent onPlayerSpotted;
    public UnityEvent onPlayerLost;
    public UnityEvent onPlayerCaught;
    public UnityEvent<float> onSuspicionChanged;

    public EnemyVisionProfile Profile { get => profile; set => profile = value; }
    public Transform Eye => eye != null ? eye : transform;
    public float LookAngle { get => lookAngle; set => lookAngle = value; }
    public float Suspicion { get; private set; }
    public bool SeesPlayer { get; private set; }
    public PlayerCharacter SeenCharacter { get; private set; }
    public bool DebugView { get => debugView; set => debugView = value; }

    static readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    static PlayerCharacter[] cachedCharacters;
    static int cacheFrame = -1;

    readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    ContactFilter2D obstacleFilter;
    bool caught;

    Transform coneTransform;
    Mesh coneMesh;
    MeshRenderer coneRenderer;
    LineRenderer debugCircle;
    Vector3[] vertices;
    Color[] colors;
    int[] triangles;

    #region Unity

    void Start()
    {
        if (profile == null)
        {
            Debug.LogWarning($"[EnemyVision] '{name}' has no EnemyVisionProfile.", this);
            enabled = false;
            return;
        }

        if (alertIcon != null) alertIcon.SetActive(false);
        CreateCone();
    }

    void Update()
    {
        RefreshPlayerCache();
        obstacleFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = profile.obstacleMask };

        PlayerCharacter seen = null;
        if (!IsBlind && !GameOver_Manager.IsGameOver)
        {
            foreach (var character in cachedCharacters)
            {
                if (character == null || !character.isActiveAndEnabled) continue;
                if (!detectUnselectedCharacter && !character.IsSelected) continue;
                if (ignorePricillaInPortal && IsHiddenByPortal(character)) continue;
                if (CanSee(character)) { seen = character; break; }
            }
        }

        UpdateSuspicion(seen);
    }

    void LateUpdate()
    {
        bool drawCone = showVisionCone || debugView;
        if (coneRenderer != null)
        {
            coneRenderer.enabled = drawCone;
            if (drawCone) BuildCone();
        }
        UpdateDebugCircle();
    }

    void OnDestroy()
    {
        if (coneMesh != null) Destroy(coneMesh);
    }

    #endregion

    #region Detection

    bool IsBlind => blindDuringTimeStop && TimeStop_Manager.IsTimeStopped;

    void UpdateSuspicion(PlayerCharacter seen)
    {
        bool wasSeeing = SeesPlayer;
        SeesPlayer = seen != null;
        SeenCharacter = seen;

        float before = Suspicion;
        if (SeesPlayer)
            Suspicion = profile.timeToDetect <= 0f ? 1f : Suspicion + Time.deltaTime / profile.timeToDetect;
        else
            Suspicion -= profile.suspicionDecay * Time.deltaTime;
        Suspicion = Mathf.Clamp01(Suspicion);

        if (!Mathf.Approximately(before, Suspicion)) onSuspicionChanged?.Invoke(Suspicion);

        if (SeesPlayer && !wasSeeing)
        {
            if (alertIcon != null) alertIcon.SetActive(true);
            onPlayerSpotted?.Invoke();
        }
        else if (!SeesPlayer && wasSeeing)
        {
            if (alertIcon != null) alertIcon.SetActive(false);
            onPlayerLost?.Invoke();
        }

        if (Suspicion >= 1f && !caught)
        {
            caught = true;
            onPlayerCaught?.Invoke();
            if (triggerGameOver) GameOver_Manager.TriggerGameOver($"Spotted by {name}!");
        }
    }

    bool CanSee(PlayerCharacter character)
    {
        Bounds b = character.Collider.bounds;
        Vector2 origin = Eye.position;
        float half = profile.visionAngle * 0.5f;

        // Check head, center and feet so peeking over cover still counts.
        for (int i = 0; i < 3; i++)
        {
            Vector2 point = new Vector2(b.center.x, b.center.y + b.extents.y * 0.8f * (1 - i));
            Vector2 to = point - origin;
            float dist = to.magnitude;
            if (dist > profile.radius || dist < 0.0001f) continue;

            bool inProximity = dist <= profile.proximityRadius;
            if (!inProximity)
            {
                float angle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(lookAngle, angle)) > half) continue;
            }

            if (CastDistance(origin, to / dist, dist) >= dist - 0.02f)
            {
                if (debugView) Debug.DrawLine(origin, point, Color.red);
                return true;
            }
        }
        return false;
    }

    static bool IsHiddenByPortal(PlayerCharacter character)
    {
        return character.ability is Teleportation_Manager tp && (tp.IsTeleporting || tp.CurrentPortal != null);
    }

    /// <summary>Distance a ray travels before an obstacle blocks it (players, triggers and this enemy are ignored).</summary>
    float CastDistance(Vector2 origin, Vector2 dir, float maxDistance)
    {
        int count = Physics2D.Raycast(origin, dir, obstacleFilter, hits, maxDistance);
        float best = maxDistance;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = hits[i].collider;
            if (playerColliders.Contains(c) || c.transform.IsChildOf(transform)) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return best;
    }

    static void RefreshPlayerCache()
    {
        if (cacheFrame == Time.frameCount) return;
        cacheFrame = Time.frameCount;
        if (cachedCharacters != null && cachedCharacters.Length > 0 && cachedCharacters[0] != null) return;

        cachedCharacters = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        playerColliders.Clear();
        foreach (var c in cachedCharacters)
            foreach (var col in c.GetComponentsInChildren<Collider2D>())
                playerColliders.Add(col);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        playerColliders.Clear();
        cachedCharacters = null;
        cacheFrame = -1;
    }

    #endregion

    #region Cone drawing

    void CreateCone()
    {
        var go = new GameObject("VisionCone");
        coneTransform = go.transform;
        coneTransform.SetParent(Eye, false);

        coneMesh = new Mesh { name = "VisionCone" };
        coneMesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = coneMesh;
        coneRenderer = go.AddComponent<MeshRenderer>();
        coneRenderer.sharedMaterial = GetMaterial();
        coneRenderer.sortingLayerName = sortingLayerName;
        coneRenderer.sortingOrder = sortingOrder;
    }

    Material GetMaterial() => coneMaterial != null ? coneMaterial : RuntimeAssets.UnlitMaterial;

    void BuildCone()
    {
        int rays = profile.rayCount;
        int vCount = rays + 2;
        if (vertices == null || vertices.Length != vCount)
        {
            vertices = new Vector3[vCount];
            colors = new Color[vCount];
            triangles = new int[rays * 3];
            for (int i = 0; i < rays; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }

        Color color = Color.Lerp(profile.idleColor, profile.alertColor, Suspicion);
        if (IsBlind) color = new Color(0.6f, 0.6f, 0.65f, profile.idleColor.a * 0.5f);
        Color edge = new Color(color.r, color.g, color.b, color.a * 0.35f);

        Vector2 origin = Eye.position;
        vertices[0] = coneTransform.InverseTransformPoint(origin);
        colors[0] = color;

        float start = lookAngle - profile.visionAngle * 0.5f;
        float step = profile.visionAngle / rays;
        for (int i = 0; i <= rays; i++)
        {
            float a = (start + step * i) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float dist = CastDistance(origin, dir, profile.radius);
            Vector2 end = origin + dir * dist;
            vertices[i + 1] = coneTransform.InverseTransformPoint(end);
            colors[i + 1] = Color.Lerp(color, edge, dist / profile.radius);
            if (debugView) Debug.DrawLine(origin, end, dist < profile.radius ? new Color(1f, 0.5f, 0f) : new Color(1f, 1f, 0f, 0.4f));
        }

        coneMesh.Clear();
        coneMesh.vertices = vertices;
        coneMesh.colors = colors;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateBounds();
    }

    void UpdateDebugCircle()
    {
        if (!debugView)
        {
            if (debugCircle != null) debugCircle.enabled = false;
            return;
        }

        if (debugCircle == null)
        {
            var go = new GameObject("VisionDebugCircle");
            go.transform.SetParent(Eye, false);
            debugCircle = go.AddComponent<LineRenderer>();
            debugCircle.useWorldSpace = true;
            debugCircle.loop = true;
            debugCircle.positionCount = 64;
            debugCircle.widthMultiplier = 0.04f;
            debugCircle.sharedMaterial = GetMaterial();
            debugCircle.sortingLayerName = sortingLayerName;
            debugCircle.sortingOrder = sortingOrder + 1;
        }

        debugCircle.enabled = true;
        debugCircle.startColor = debugCircle.endColor = debugCircleColor;
        Vector3 c = Eye.position;
        for (int i = 0; i < 64; i++)
        {
            float a = i / 64f * Mathf.PI * 2f;
            debugCircle.SetPosition(i, c + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * profile.radius);
        }
    }

    #endregion

    #region Gizmos

    void OnDrawGizmos()
    {
        if (debugView) DrawGizmos(0.6f);
    }

    void OnDrawGizmosSelected() => DrawGizmos(1f);

    void DrawGizmos(float alpha)
    {
        if (profile == null) return;
        Vector3 c = Eye.position;

        Gizmos.color = new Color(debugCircleColor.r, debugCircleColor.g, debugCircleColor.b, debugCircleColor.a * alpha);
        DrawCircle(c, profile.radius);
        if (profile.proximityRadius > 0f) DrawCircle(c, profile.proximityRadius);

        Gizmos.color = new Color(1f, 0.85f, 0.2f, alpha);
        float half = profile.visionAngle * 0.5f;
        Vector3 a = Dir(lookAngle - half) * profile.radius;
        Vector3 b = Dir(lookAngle + half) * profile.radius;
        Gizmos.DrawLine(c, c + a);
        Gizmos.DrawLine(c, c + b);
        Gizmos.DrawLine(c, c + Dir(lookAngle) * profile.radius * 0.35f);

        // Arc
        int seg = Mathf.Max(4, Mathf.CeilToInt(profile.visionAngle / 6f));
        Vector3 prev = c + a;
        for (int i = 1; i <= seg; i++)
        {
            Vector3 p = c + Dir(lookAngle - half + profile.visionAngle * i / seg) * profile.radius;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    static Vector3 Dir(float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f);
    }

    static void DrawCircle(Vector3 center, float radius)
    {
        Vector3 prev = center + new Vector3(radius, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 p = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    #endregion
}
