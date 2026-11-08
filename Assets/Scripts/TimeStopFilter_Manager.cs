using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Screen-wide "time stop" color filter.
///
/// What gets filtered:   everything the main (base) camera renders, including Screen Space - Camera / World Space
///                       canvases (HP bar, enemy indicators...).
/// What is NOT filtered: objects on the IgnoreTimeStop layer (rendered by an auto-created overlay camera),
///                       objects tagged "Tetra" / "IgnoreTimeStop" (moved to that layer automatically),
///                       and Screen Space - Overlay canvases (use this for the pause menu).
///
/// Requires TimeStopFilterFeature on the Renderer2D asset.
/// Call Activate / Deactivate / Toggle / ActivateFor from a Button OnClick, a UnityEvent, a DOTween callback
/// (e.g. tween.OnComplete(TimeStopFilter_Manager.Instance.Activate)) or any script.
/// </summary>
[DisallowMultipleComponent]
public class TimeStopFilter_Manager : MonoBehaviour
{
    public enum ColorMode
    {
        /// <summary>Monochrome tinted with one color (black -> color, optionally -> white).</summary>
        SingleColor,
        /// <summary>Monochrome mapped through a gradient, dark -> bright (mix several colors).</summary>
        MultiColorGradient
    }

    public enum TransitionStyle
    {
        Fade,
        /// <summary>Filter expands as a circle from the origin (player / screen center).</summary>
        RadialWipe
    }

    public static TimeStopFilter_Manager Instance { get; private set; }

    [Header("Color")]
    [SerializeField] ColorMode colorMode = ColorMode.SingleColor;
    [SerializeField] Color tintColor = new Color(0.55f, 0.8f, 1f);
    [Tooltip("Single color mode: brightest areas go to white instead of the tint color.")]
    [SerializeField] bool keepHighlights = true;
    [Tooltip("Multi color mode: left = shadows, right = highlights. Add keys to mix colors.")]
    [SerializeField] Gradient colorGradient = DefaultGradient();
    [Range(0f, 3f)] [SerializeField] float contrast = 1.15f;
    [Range(-1f, 1f)] [SerializeField] float brightness = 0f;
    [Tooltip("How strongly the filter replaces the original colors when fully active.")]
    [Range(0f, 1f)] [SerializeField] float maxIntensity = 1f;

    [Header("Transition")]
    [SerializeField] TransitionStyle transition = TransitionStyle.RadialWipe;
    [Min(0f)] [SerializeField] float enterDuration = 0.45f;
    [Min(0f)] [SerializeField] float exitDuration = 0.35f;
    [SerializeField] Ease enterEase = Ease.OutCubic;
    [SerializeField] Ease exitEase = Ease.InCubic;
    [Tooltip("Radial wipe origin. Empty = screen center.")]
    [SerializeField] Transform radialOrigin;
    [Range(0.001f, 1f)] [SerializeField] float radialSoftness = 0.08f;
    [SerializeField] Color edgeGlowColor = Color.white;
    [Range(0f, 2f)] [SerializeField] float edgeGlowStrength = 0.6f;
    [Tooltip("Keep animating when Time.timeScale = 0.")]
    [SerializeField] bool useUnscaledTime = true;

    [Header("Ignore (not affected by the filter)")]
    [SerializeField] string ignoreLayerName = "IgnoreTimeStop";
    [SerializeField] string[] ignoreTags = { "Tetra", "IgnoreTimeStop" };
    [Tooltip("Move objects with the ignore tags (and their children) onto the ignore layer.\n" +
             "Note: this changes their GameObject layer, which also affects physics / raycast layer masks.")]
    [SerializeField] bool autoAssignIgnoreLayer = true;
    [SerializeField] bool includeChildren = true;

    [Header("Cameras")]
    [Tooltip("Empty = Camera.main.")]
    [SerializeField] Camera mainCamera;
    [Tooltip("Create an overlay camera that renders only the ignore layer on top of the filtered image.")]
    [SerializeField] bool autoSetupIgnoreCamera = true;

    [Header("Events")]
    public UnityEvent onTimeStopStart;
    public UnityEvent onTimeStopFullyActive;
    public UnityEvent onTimeStopEnd;
    public UnityEvent onTimeStopFullyInactive;

    public bool IsActive { get; private set; }
    /// <summary>0 = off, 1 = fully on.</summary>
    public float Progress => progress;

    static readonly int IntensityId = Shader.PropertyToID("_TimeStopIntensity");
    static readonly int GradientTexId = Shader.PropertyToID("_TimeStopGradientTex");
    static readonly int ParamsId = Shader.PropertyToID("_TimeStopParams");
    static readonly int RadialId = Shader.PropertyToID("_TimeStopRadial");
    static readonly int EdgeId = Shader.PropertyToID("_TimeStopEdge");

    const int GradientResolution = 256;

    float progress;
    Texture2D gradientTex;
    Camera ignoreCamera;
    DG.Tweening.Tween progressTween;
    DG.Tweening.Tween autoOffTween;

    #region Unity

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[TimeStopFilter] More than one TimeStopFilter_Manager in the scene. Using '{Instance.name}'.", this);
        }
        else Instance = this;

        BakeGradient();
        Apply();
    }

    void Start()
    {
        if (autoSetupIgnoreCamera) SetupIgnoreCamera();
        RefreshIgnoredObjects();
    }

    void LateUpdate()
    {
        if (ignoreCamera != null && mainCamera != null)
        {
            ignoreCamera.orthographic = mainCamera.orthographic;
            ignoreCamera.orthographicSize = mainCamera.orthographicSize;
            ignoreCamera.fieldOfView = mainCamera.fieldOfView;
        }

        if (progress > 0f) Apply(); // keep following the radial origin / screen aspect
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        BakeGradient();
        Apply();
    }

    void OnDisable()
    {
        KillTweens();
        Shader.SetGlobalFloat(IntensityId, 0f); // globals persist in the editor, so always clear them
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (gradientTex != null) Destroy(gradientTex);
    }

    #endregion

    #region Public API (Button / UnityEvent / DOTween callback friendly)

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        RefreshIgnoredObjects(); // catch Tetra objects spawned since the last time
        onTimeStopStart?.Invoke();
        TweenProgress(1f, enterDuration, enterEase, () => onTimeStopFullyActive?.Invoke());
    }

    public void Deactivate()
    {
        autoOffTween?.Kill();
        if (!IsActive) return;
        IsActive = false;
        onTimeStopEnd?.Invoke();
        TweenProgress(0f, exitDuration, exitEase, () => onTimeStopFullyInactive?.Invoke());
    }

    public void Toggle()
    {
        if (IsActive) Deactivate();
        else Activate();
    }

    /// <summary>Activate, then automatically deactivate after <paramref name="seconds"/> (counted after the enter transition).</summary>
    public void ActivateFor(float seconds)
    {
        Activate();
        autoOffTween?.Kill();
        autoOffTween = DOVirtual.DelayedCall(enterDuration + seconds, Deactivate, useUnscaledTime).SetLink(gameObject);
    }

    public void ActivateInstant()
    {
        KillTweens();
        bool wasActive = IsActive;
        IsActive = true;
        RefreshIgnoredObjects();
        progress = 1f;
        Apply();
        if (!wasActive) onTimeStopStart?.Invoke();
        onTimeStopFullyActive?.Invoke();
    }

    public void DeactivateInstant()
    {
        KillTweens();
        bool wasActive = IsActive;
        IsActive = false;
        progress = 0f;
        Apply();
        if (wasActive) onTimeStopEnd?.Invoke();
        onTimeStopFullyInactive?.Invoke();
    }

    /// <summary>Returns the enter tween so it can be chained / awaited in DOTween sequences.</summary>
    public DG.Tweening.Tween ActivateTween()
    {
        Activate();
        return progressTween;
    }

    /// <summary>Returns the exit tween so it can be chained / awaited in DOTween sequences.</summary>
    public DG.Tweening.Tween DeactivateTween()
    {
        Deactivate();
        return progressTween;
    }

    /// <summary>Switch to single color mode with this tint.</summary>
    public void SetTintColor(Color color)
    {
        colorMode = ColorMode.SingleColor;
        tintColor = color;
        BakeGradient();
    }

    /// <summary>Switch to multi color mode with this gradient.</summary>
    public void SetGradient(Gradient gradient)
    {
        if (gradient == null) return;
        colorMode = ColorMode.MultiColorGradient;
        colorGradient = gradient;
        BakeGradient();
    }

    /// <summary>Mix any number of colors, spread evenly from shadows (first) to highlights (last).</summary>
    public void SetColors(params Color[] colors)
    {
        if (colors == null || colors.Length == 0) return;
        if (colors.Length == 1)
        {
            SetTintColor(colors[0]);
            return;
        }

        colorMode = ColorMode.MultiColorGradient;
        BakeFromFunction(t =>
        {
            float f = t * (colors.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), colors.Length - 2);
            return Color.Lerp(colors[i], colors[i + 1], f - i);
        });
    }

    public void SetMaxIntensity(float value)
    {
        maxIntensity = Mathf.Clamp01(value);
        Apply();
    }

    public void SetRadialOrigin(Transform origin) => radialOrigin = origin;

    /// <summary>Exclude a runtime-spawned object (and its children) from the filter.</summary>
    public void RegisterIgnored(GameObject go)
    {
        int layer = LayerMask.NameToLayer(ignoreLayerName);
        if (go == null || layer < 0) return;
        SetLayer(go, layer);
    }

    /// <summary>Re-scan the scene for objects with the ignore tags and move them to the ignore layer.</summary>
    public void RefreshIgnoredObjects()
    {
        if (!autoAssignIgnoreLayer) return;

        int layer = LayerMask.NameToLayer(ignoreLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[TimeStopFilter] Layer '{ignoreLayerName}' does not exist. Use Tools > Screen Filters > Create TimeStop Tags & Layer.", this);
            return;
        }

        foreach (string tag in ignoreTags)
        {
            if (string.IsNullOrEmpty(tag)) continue;

            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(tag); }
            catch (UnityException) { continue; } // tag not defined in the Tag Manager

            foreach (var go in found) SetLayer(go, layer);
        }
    }

    #endregion

    #region Internals

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

    void KillTweens()
    {
        progressTween?.Kill();
        autoOffTween?.Kill();
    }

    void Apply()
    {
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        Vector2 center = GetRadialCenter();

        float intensity;
        float radius;
        if (transition == TransitionStyle.Fade)
        {
            intensity = progress * maxIntensity;
            radius = 100f; // covers the whole screen
        }
        else
        {
            intensity = progress > 0f ? maxIntensity : 0f;
            radius = progress * (MaxDistanceToCorner(center, aspect) + radialSoftness);
        }

        Shader.SetGlobalFloat(IntensityId, intensity);
        Shader.SetGlobalVector(ParamsId, new Vector4(contrast, brightness,
            QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f : 0f, aspect));
        Shader.SetGlobalVector(RadialId, new Vector4(center.x, center.y, radius, radialSoftness));

        float glow = transition == TransitionStyle.RadialWipe && progress < 1f ? edgeGlowStrength : 0f;
        Shader.SetGlobalVector(EdgeId, new Vector4(edgeGlowColor.r, edgeGlowColor.g, edgeGlowColor.b, glow));
        if (gradientTex != null) Shader.SetGlobalTexture(GradientTexId, gradientTex);
    }

    Vector2 GetRadialCenter()
    {
        if (radialOrigin == null) return new Vector2(0.5f, 0.5f);
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null) return new Vector2(0.5f, 0.5f);
        Vector3 vp = cam.WorldToViewportPoint(radialOrigin.position);
        return new Vector2(vp.x, vp.y);
    }

    static float MaxDistanceToCorner(Vector2 c, float aspect)
    {
        float dx = Mathf.Max(c.x, 1f - c.x) * aspect;
        float dy = Mathf.Max(c.y, 1f - c.y);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    void BakeGradient()
    {
        if (colorMode == ColorMode.SingleColor)
        {
            Color tint = tintColor;
            BakeFromFunction(t => keepHighlights
                ? (t < 0.75f ? Color.Lerp(Color.black, tint, t / 0.75f) : Color.Lerp(tint, Color.white, (t - 0.75f) / 0.25f))
                : Color.Lerp(Color.black, tint, t));
        }
        else
        {
            Gradient g = colorGradient ?? DefaultGradient();
            BakeFromFunction(g.Evaluate);
        }
    }

    void BakeFromFunction(System.Func<float, Color> evaluate)
    {
        if (gradientTex == null)
        {
            // sRGB texture: colors are authored in gamma space, the GPU converts them on sampling.
            gradientTex = new Texture2D(GradientResolution, 1, TextureFormat.RGBA32, false, false)
            {
                name = "TimeStopGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
        }

        var pixels = new Color[GradientResolution];
        for (int i = 0; i < GradientResolution; i++)
            pixels[i] = evaluate(i / (GradientResolution - 1f));

        gradientTex.SetPixels(pixels);
        gradientTex.Apply(false);
        Shader.SetGlobalTexture(GradientTexId, gradientTex);
    }

    void SetupIgnoreCamera()
    {
        int layer = LayerMask.NameToLayer(ignoreLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[TimeStopFilter] Layer '{ignoreLayerName}' does not exist, ignore camera not created.", this);
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[TimeStopFilter] No main camera found, ignore camera not created.", this);
            return;
        }

        var baseData = mainCamera.GetUniversalAdditionalCameraData();
        if (baseData.renderType != CameraRenderType.Base)
        {
            Debug.LogWarning("[TimeStopFilter] Main camera must be a Base camera.", this);
            return;
        }

        mainCamera.cullingMask &= ~(1 << layer);

        var go = new GameObject("TimeStop_IgnoreCamera");
        go.transform.SetParent(mainCamera.transform, false);
        ignoreCamera = go.AddComponent<Camera>();
        ignoreCamera.CopyFrom(mainCamera);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        ignoreCamera.cullingMask = 1 << layer;
        ignoreCamera.targetTexture = null;

        var data = ignoreCamera.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Overlay;
        data.renderPostProcessing = false;
        baseData.cameraStack.Add(ignoreCamera);
    }

    void SetLayer(GameObject go, int layer)
    {
        if (!includeChildren)
        {
            go.layer = layer;
            return;
        }

        var stack = new Stack<Transform>();
        stack.Push(go.transform);
        while (stack.Count > 0)
        {
            Transform t = stack.Pop();
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
        }
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.05f, 0.05f, 0.2f), 0f),
                new GradientColorKey(new Color(0.25f, 0.45f, 0.9f), 0.45f),
                new GradientColorKey(new Color(0.6f, 1f, 0.95f), 0.8f),
                new GradientColorKey(Color.white, 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    #endregion
}
