using UnityEngine;

/// <summary>
/// Draws sand inside an hourglass sprite. Put it on the hourglass frame (Hourglass_0).
/// It builds its own children at runtime:
///   - a SpriteMask shaped like the hourglass sprite,
///   - a top sand block that drains toward the neck,
///   - a bottom sand block that piles up,
///   - an optional thin falling stream.
/// The mask clips the blocks into the triangle shapes, so it looks like a real hourglass.
/// Set Fill (0 = empty bottom / full top, 1 = full bottom / empty top) from code, e.g. TimeStop_Manager.
/// Works best inside a SortingGroup (PlayerCharacter adds one), which keeps the mask local to the character.
/// </summary>
[DisallowMultipleComponent]
public class HourglassFill : MonoBehaviour
{
    public enum FillMode
    {
        /// <summary>Top bulb drains while the bottom bulb fills.</summary>
        Hourglass,
        /// <summary>One block fills bottom to top (like a slider), still clipped to the hourglass shape.</summary>
        SimpleVertical
    }

    [SerializeField] SpriteRenderer frame;
    [SerializeField] FillMode mode = FillMode.Hourglass;
    [SerializeField] Color sandColor = new Color(1f, 0.82f, 0.35f, 1f);
    [Tooltip("Thickness of the glass at the top and bottom, as a fraction of the sprite height.")]
    [Range(0f, 0.3f)] [SerializeField] float glassPadding = 0.06f;
    [Tooltip("Shift of the narrow middle point, as a fraction of the sprite height (0 = exact middle).")]
    [Range(-0.3f, 0.3f)] [SerializeField] float neckOffset = 0f;
    [Tooltip("Sand pixels are drawn this many orders in front of the frame.")]
    [SerializeField] int sandSortingOffset = 1;
    [Tooltip("Alpha cutoff of the mask (raise it if sand shows outside a soft-edged sprite).")]
    [Range(0f, 1f)] [SerializeField] float maskAlphaCutoff = 0.2f;

    [Header("Falling Stream")]
    [SerializeField] bool showStream = true;
    [Tooltip("Width of the stream as a fraction of the sprite width.")]
    [Range(0.005f, 0.2f)] [SerializeField] float streamWidth = 0.035f;

    [Header("Preview")]
    [Range(0f, 1f)] [SerializeField] float fill = 0f;

    public float Fill => fill;
    /// <summary>Show the stream (e.g. only while recharging).</summary>
    public bool StreamActive { get; set; } = true;
    public Color SandColor
    {
        get => sandColor;
        set { sandColor = value; ApplyColors(); }
    }

    SpriteMask mask;
    SpriteRenderer topSand, bottomSand, stream;
    bool built;

    void Awake()
    {
        Build();
        SetFill(fill);
    }

    void OnValidate()
    {
        if (!Application.isPlaying || !built) return;
        ApplyColors();
        SetFill(fill);
    }

    public void SetFill(float value)
    {
        fill = Mathf.Clamp01(value);
        if (!built || frame == null || frame.sprite == null) return;

        Bounds b = frame.sprite.bounds; // local space of the frame
        float pad = glassPadding * b.size.y;
        float neck = b.center.y + neckOffset * b.size.y;
        float innerTop = b.max.y - pad;
        float innerBottom = b.min.y + pad;
        float width = b.size.x;

        if (mode == FillMode.Hourglass)
        {
            float bottomH = fill * (neck - innerBottom);
            float topH = (1f - fill) * (innerTop - neck);
            SetBlock(bottomSand, b.center.x, innerBottom, width, bottomH);
            SetBlock(topSand, b.center.x, neck, width, topH);

            bool streamOn = showStream && StreamActive && fill > 0.001f && fill < 0.999f;
            SetBlock(stream, b.center.x, innerBottom + bottomH, streamWidth * width, streamOn ? neck - (innerBottom + bottomH) : 0f);
        }
        else
        {
            SetBlock(bottomSand, b.center.x, innerBottom, width, fill * (innerTop - innerBottom));
            SetBlock(topSand, 0f, 0f, 0f, 0f);
            SetBlock(stream, 0f, 0f, 0f, 0f);
        }
    }

    void Build()
    {
        if (built) return;
        if (frame == null) frame = GetComponent<SpriteRenderer>();
        if (frame == null || frame.sprite == null)
        {
            Debug.LogWarning("[HourglassFill] Needs a SpriteRenderer with the hourglass sprite.", this);
            return;
        }


        var maskGo = new GameObject("Hourglass_Mask");
        maskGo.transform.SetParent(transform, false);
        mask = maskGo.AddComponent<SpriteMask>();
        mask.sprite = frame.sprite;
        mask.alphaCutoff = maskAlphaCutoff;

        topSand = CreateSand("Hourglass_SandTop");
        bottomSand = CreateSand("Hourglass_SandBottom");
        stream = CreateSand("Hourglass_SandStream");

        built = true;
        ApplyColors();
    }

    SpriteRenderer CreateSand(string objName)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeAssets.WhiteSprite;
        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        sr.sortingLayerID = frame.sortingLayerID;
        sr.sortingOrder = frame.sortingOrder + sandSortingOffset;
        return sr;
    }

    void ApplyColors()
    {
        if (topSand != null) topSand.color = sandColor;
        if (bottomSand != null) bottomSand.color = sandColor;
        if (stream != null) stream.color = sandColor;
    }

    static void SetBlock(SpriteRenderer sr, float centerX, float bottomY, float width, float height)
    {
        if (sr == null) return;
        bool visible = height > 0.0001f && width > 0.0001f;
        sr.enabled = visible;
        if (!visible) return;
        sr.transform.localPosition = new Vector3(centerX, bottomY + height * 0.5f, 0f);
        sr.transform.localScale = new Vector3(width, height, 1f);
    }
}
