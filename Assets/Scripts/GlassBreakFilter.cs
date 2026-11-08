using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Screen-wide broken-glass effect (URP port of Moein Saadati's "Broken Screen" image effect).
/// Shards refract the screen outward from the impact point and the crack texture is added on top.
///
/// Requires GlassBreakFilterFeature on the Renderer2D asset.
/// Call Break / Repair / Toggle / BreakFor from a Button OnClick, a UnityEvent, a DOTween callback or any script.
///
/// Direction map: generate one from your crack texture with Tools > Screen Filters > Glass Direction Map Generator.
/// Without one, the screen is pushed radially outward from the impact point (smooth instead of per-shard).
/// </summary>
[DisallowMultipleComponent]
public class GlassBreakFilter : MonoBehaviour
{
    public static GlassBreakFilter Instance { get; private set; }

    [Header("Textures")]
    [Tooltip("Diffuse crack texture (white cracks on black), e.g. GlassBreak_1920x1080px_Shader.")]
    [SerializeField] Texture2D crackTexture;
    [Tooltip("Optional direction map (RG = shard direction, 0.5 grey = no movement).")]
    [SerializeField] Texture2D directionMap;
    [Tooltip("Flip the crack/direction textures vertically if they show upside down on your platform.")]
    [SerializeField] bool flipY = false;

    [Header("Look")]
    [Tooltip("How far shards push the image (the article's _Refraction). Negative pulls inward.")]
    [Range(-0.2f, 0.2f)] [SerializeField] float refraction = 0.035f;
    [Range(0f, 3f)] [SerializeField] float crackBrightness = 1f;
    [SerializeField] Color crackTint = Color.white;
    [Tooltip("RGB split along the shard direction.")]
    [Range(0f, 1f)] [SerializeField] float chromaticAberration = 0.25f;
    [Tooltip("Impact point in viewport space (0..1). Used by the no-direction-map fallback.")]
    [SerializeField] Vector2 impactPoint = new Vector2(0.5f, 0.5f);
    [Tooltip("Fallback only: radius around the impact point that stays undistorted (the hole in your crack texture).")]
    [Range(0f, 1f)] [SerializeField] float holeRadius = 0.35f;

    [Header("Animation")]
    [Min(0f)] [SerializeField] float breakDuration = 0.18f;
    [Tooltip("OutBack overshoots, so shards 'shoot out' then settle.")]
    [SerializeField] Ease breakEase = Ease.OutBack;
    [Min(0f)] [SerializeField] float repairDuration = 0.4f;
    [SerializeField] Ease repairEase = Ease.InQuad;
    [SerializeField] bool useUnscaledTime = true;

    [Header("Events")]
    public UnityEvent onBreak;
    public UnityEvent onFullyBroken;
    public UnityEvent onRepair;
    public UnityEvent onFullyRepaired;

    public bool IsBroken { get; private set; }
    public float Progress => progress;

    static readonly int IntensityId = Shader.PropertyToID("_GlassBreakIntensity");
    static readonly int DiffuseId = Shader.PropertyToID("_GlassBreakDiffuseTex");
    static readonly int DirectionId = Shader.PropertyToID("_GlassBreakDirectionTex");
    static readonly int ParamsId = Shader.PropertyToID("_GlassBreakParams");
    static readonly int TintId = Shader.PropertyToID("_GlassBreakTint");
    static readonly int UVId = Shader.PropertyToID("_GlassBreakUV");
    static readonly int CenterId = Shader.PropertyToID("_GlassBreakCenter");

    float progress;
    DG.Tweening.Tween progressTween;
    DG.Tweening.Tween autoRepairTween;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning($"[GlassBreakFilter] More than one GlassBreakFilter in the scene. Using '{Instance.name}'.", this);
        else Instance = this;

        if (crackTexture == null)
            Debug.LogWarning("[GlassBreakFilter] No crack texture assigned.", this);

        Apply();
    }

    void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }

    void LateUpdate()
    {
        if (progress != 0f) Apply(); // keeps aspect correct if the window is resized
    }

    void OnDisable()
    {
        progressTween?.Kill();
        autoRepairTween?.Kill();
        Shader.SetGlobalFloat(IntensityId, 0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    #region Public API

    public void Break()
    {
        autoRepairTween?.Kill();
        if (IsBroken) return;
        IsBroken = true;
        onBreak?.Invoke();
        TweenProgress(1f, breakDuration, breakEase, () => onFullyBroken?.Invoke());
    }

    /// <summary>Break at a world position (e.g. where the hit landed). Only affects the no-direction-map fallback.</summary>
    public void BreakAt(Vector3 worldPosition)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(worldPosition);
            impactPoint = new Vector2(vp.x, vp.y);
        }
        Break();
    }

    public void Repair()
    {
        autoRepairTween?.Kill();
        if (!IsBroken) return;
        IsBroken = false;
        onRepair?.Invoke();
        TweenProgress(0f, repairDuration, repairEase, () => onFullyRepaired?.Invoke());
    }

    public void Toggle()
    {
        if (IsBroken) Repair();
        else Break();
    }

    /// <summary>Break, then repair automatically after <paramref name="seconds"/>.</summary>
    public void BreakFor(float seconds)
    {
        Break();
        autoRepairTween?.Kill();
        autoRepairTween = DOVirtual.DelayedCall(breakDuration + seconds, Repair, useUnscaledTime).SetLink(gameObject);
    }

    public void BreakInstant()
    {
        progressTween?.Kill();
        autoRepairTween?.Kill();
        bool was = IsBroken;
        IsBroken = true;
        progress = 1f;
        Apply();
        if (!was) onBreak?.Invoke();
        onFullyBroken?.Invoke();
    }

    public void RepairInstant()
    {
        progressTween?.Kill();
        autoRepairTween?.Kill();
        bool was = IsBroken;
        IsBroken = false;
        progress = 0f;
        Apply();
        if (was) onRepair?.Invoke();
        onFullyRepaired?.Invoke();
    }

    public DG.Tweening.Tween BreakTween()
    {
        Break();
        return progressTween;
    }

    public DG.Tweening.Tween RepairTween()
    {
        Repair();
        return progressTween;
    }

    public void SetCrackTexture(Texture2D crack, Texture2D direction = null)
    {
        crackTexture = crack;
        directionMap = direction;
        Apply();
    }

    public void SetRefraction(float value)
    {
        refraction = value;
        Apply();
    }

    #endregion

    void TweenProgress(float target, float duration, Ease ease, TweenCallback onComplete)
    {
        progressTween?.Kill();
        if (duration <= 0f)
        {
            progress = target;
            Apply();
            onComplete?.Invoke();
            return;
        }

        progressTween = DOTween.To(() => progress, v => { progress = v; Apply(); }, target, duration)
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject)
            .OnComplete(onComplete);
    }

    void Apply()
    {
        // Intensity is clamped in the shader; refraction uses the raw progress so OutBack overshoot pushes shards further.
        Shader.SetGlobalFloat(IntensityId, crackTexture != null || directionMap != null ? Mathf.Max(progress, 0f) : 0f);

        if (crackTexture != null) Shader.SetGlobalTexture(DiffuseId, crackTexture);
        else Shader.SetGlobalTexture(DiffuseId, Texture2D.blackTexture);
        Shader.SetGlobalTexture(DirectionId, directionMap != null ? directionMap : Texture2D.grayTexture);

        float overshoot = Mathf.Max(progress, 1f);
        Shader.SetGlobalVector(ParamsId, new Vector4(refraction * overshoot, crackBrightness, chromaticAberration,
            directionMap != null ? 1f : 0f));
        Shader.SetGlobalColor(TintId, crackTint);
        Shader.SetGlobalVector(UVId, flipY ? new Vector4(1f, -1f, 0f, 1f) : new Vector4(1f, 1f, 0f, 0f));

        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        Shader.SetGlobalVector(CenterId, new Vector4(impactPoint.x, impactPoint.y, holeRadius, aspect));
    }
}
