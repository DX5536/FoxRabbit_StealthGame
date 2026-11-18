using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable]
public class CreditEntry
{
    public string title;
    [TextArea(2, 6)] public string body;
}

/// <summary>
/// Credits hidden inside the artwork. Shatter() breaks the art panel into glass: the biggest pieces carry one credit each,
/// the rest float around as debris. Hover = parallax + lift, click a piece = zoom in to read it,
/// mouse wheel / arrow keys step to the next piece while zoomed, right click / Esc zooms out. Reassemble() puts the art back.
/// Like FancyScrollView, one continuous focus position drives every piece, so stepping slides one out as the next comes in.
/// Pieces are made at runtime, so add or remove credits freely. The RawImage shows the intact art (also in the editor).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ShatterCredits : MonoBehaviour
{
    [Header("Art")]
    [Tooltip("Shows the intact artwork (stretched over this panel). Hidden while shattered.")]
    [SerializeField] RawImage artwork;
    [Tooltip("Optional: faded in once the glass has settled (e.g. a Back button).")]
    [SerializeField] CanvasGroup showWhenShattered;

    [Header("Credits (one glass piece each)")]
    [SerializeField] List<CreditEntry> credits = new List<CreditEntry>();
    [SerializeField] TMP_FontAsset font;
    [Tooltip("Credit pieces turn this colour (multiplied over the art) so the text is readable.")]
    [SerializeField] Color glassTint = new Color(0.17f, 0.2f, 0.4f, 0.92f);

    [Header("Shatter")]
    [SerializeField] int debrisPieces = 30;
    [Tooltip("Where the glass breaks, 0-1 inside the panel.")]
    [SerializeField] Vector2 impactPoint = new Vector2(0.3f, 0.55f);
    [SerializeField] int seed = 7;
    [SerializeField] float duration = 1.1f;
    [Tooltip("0 = every piece moves at once, 1 = a wave travels out from the impact.")]
    [Range(0f, 1f)] [SerializeField] float ripple = 0.45f;
    [SerializeField] float debrisSpread = 650f;
    [Range(0f, 1f)] [SerializeField] float debrisAlpha = 0.55f;

    [Header("Browse")]
    [SerializeField] float parallax = 40f;
    [SerializeField] float hoverScale = 1.1f;
    [Tooltip("Size of the zoomed piece as a fraction of the panel's short side.")]
    [SerializeField] float zoomSize = 0.9f;
    [SerializeField] float smoothing = 10f;

    [Header("Events")]
    public UnityEvent onShattered = new UnityEvent();
    public UnityEvent onReassembled = new UnityEvent();
    [Tooltip("A credit piece was zoomed in (credit index).")]
    public UnityEvent<int> onFocus = new UnityEvent<int>();

    class Piece
    {
        public GlassShard g;
        public TMP_Text text;
        public int credit = -1; // -1 = debris
        public Vector2 home, rest, dir, textBox;
        public float restRot, restScale = 1f, zoomScale = 1f, depth, delay, spin, hover;
        public bool hovered;
    }

    readonly List<Piece> pieces = new List<Piece>();
    readonly List<Piece> creditPieces = new List<Piece>();
    RectTransform container;
    float t, target;          // shatter progress 0..1
    float zoom, focusPos;
    int focusIndex;
    bool zoomed;
    Vector2 mouse;

    RectTransform Rt => (RectTransform)transform;
    public bool IsShattered => target > 0.5f;

    void Awake()
    {
        if (artwork != null) artwork.uvRect = CoverUV(artwork.texture, Rt.rect);
        if (showWhenShattered != null) SetGroup(0f);
    }

    void OnValidate()
    {
        if (artwork != null) artwork.uvRect = CoverUV(artwork.texture, Rt.rect);
    }

    public void Shatter()
    {
        if (pieces.Count == 0) Build();
        target = 1f;
        if (artwork != null) artwork.enabled = false;
        container.gameObject.SetActive(true);
    }

    public void Reassemble()
    {
        target = 0f;
        zoomed = false;
    }

    void Update()
    {
        if (container == null || !container.gameObject.activeSelf) return;
        float dt = Time.unscaledDeltaTime;
        float k = 1f - Mathf.Exp(-smoothing * dt);

        if (t != target)
        {
            t = Mathf.MoveTowards(t, target, dt / Mathf.Max(0.01f, duration));
            if (t == 1f) onShattered?.Invoke();
            else if (t == 0f)
            {
                if (artwork != null) artwork.enabled = true;
                container.gameObject.SetActive(false);
                onReassembled?.Invoke();
                return;
            }
        }
        bool settled = t == 1f;
        if (settled) HandleKeys();
        if (showWhenShattered != null) SetGroup(Mathf.SmoothStep(0f, 1f, (t - 0.8f) / 0.2f));

        zoom = Mathf.Lerp(zoom, zoomed ? 1f : 0f, k);
        focusPos = Mathf.Lerp(focusPos, focusIndex, k);
        mouse = Vector2.Lerp(mouse, MouseInPanel(), k);

        foreach (var p in pieces)
        {
            float local = Mathf.Clamp01(t * (1f + ripple) - p.delay * ripple);
            float e = 1f - Mathf.Pow(1f - local, 3f); // out-cubic
            float arc = Mathf.Sin(Mathf.PI * e);

            Vector2 pos = Vector2.LerpUnclamped(p.home, p.rest, e) + p.dir * (90f * arc) - mouse * (parallax * p.depth * e);
            float rot = p.restRot * e + p.spin * arc;
            float scale = Mathf.Lerp(1f, p.restScale, e);
            Color c = Color.white;

            if (p.credit >= 0)
            {
                p.hover = Mathf.Lerp(p.hover, p.hovered && settled && !zoomed ? 1f : 0f, k);
                float f = zoom * Mathf.Clamp01(1f - Mathf.Abs(p.credit - focusPos));
                scale *= Mathf.Lerp(1f, hoverScale, p.hover) * (1f - 0.25f * zoom * (1f - f));
                pos = Vector2.Lerp(pos, Vector2.zero, f);
                rot = Mathf.LerpAngle(rot, 0f, f);
                scale = Mathf.Lerp(scale, p.zoomScale, f);
                c = Color.Lerp(Color.white, glassTint, e);
                c.a *= 1f - 0.6f * zoom * (1f - f);
                p.text.alpha = Mathf.SmoothStep(0f, 1f, (local - 0.75f) / 0.25f) * (1f - 0.7f * zoom * (1f - f));
                p.g.EdgeGlow = Mathf.Clamp01(local * 4f) * Mathf.Lerp(0.35f, 0.7f, Mathf.Max(p.hover, f));
            }
            else
            {
                c.a = Mathf.Lerp(1f, debrisAlpha, e) * (1f - 0.5f * zoom);
                p.g.EdgeGlow = Mathf.Clamp01(local * 4f) * 0.35f;
            }

            var rt = p.g.rectTransform;
            rt.anchoredPosition = pos;
            rt.localEulerAngles = new Vector3(0f, 0f, rot);
            rt.localScale = new Vector3(scale, scale, 1f);
            p.g.color = c;
        }
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        var m = Mouse.current;
        if (!zoomed) return;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (m != null && m.rightButton.wasPressedThisFrame)) { zoomed = false; return; }
        int step = 0;
        if (kb != null)
        {
            if (kb.rightArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step++;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step--;
        }
        if (m != null) step -= Math.Sign(m.scroll.ReadValue().y);
        if (step != 0) Focus(Mathf.Clamp(focusIndex + step, 0, creditPieces.Count - 1));
    }

    void Focus(int index)
    {
        if (index < 0 || index >= creditPieces.Count) return;
        if (!zoomed) focusPos = index; // jump straight to it, then just zoom
        focusIndex = index;
        zoomed = true;
        creditPieces[index].g.transform.SetAsLastSibling();
        onFocus?.Invoke(index);
    }

    void OnPieceClicked(Piece p, PointerEventData e)
    {
        if (t != 1f || e.button != PointerEventData.InputButton.Left) return;
        if (zoomed && p.credit == focusIndex) zoomed = false;
        else Focus(p.credit);
    }

    Vector2 MouseInPanel()
    {
        var m = Mouse.current;
        if (m == null) return Vector2.zero;
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Rect r = Rt.rect;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(Rt, m.position.ReadValue(), cam, out var lp);
        Vector2 n = (lp - r.center) / (r.size * 0.5f);
        return new Vector2(Mathf.Clamp(n.x, -1f, 1f), Mathf.Clamp(n.y, -1f, 1f));
    }

    void SetGroup(float a)
    {
        showWhenShattered.alpha = a;
        showWhenShattered.interactable = showWhenShattered.blocksRaycasts = a > 0.99f;
    }

    #region Building the pieces

    void Build()
    {
        container = new GameObject("Shards", typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(transform, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = container.offsetMax = Vector2.zero;
        container.SetSiblingIndex(artwork != null ? artwork.transform.GetSiblingIndex() + 1 : 0);

        Rect r = Rt.rect;
        Texture tex = artwork != null ? artwork.texture : null;
        Rect uvRect = CoverUV(tex, r);
        var rng = new System.Random(seed);
        float Rand() => (float)rng.NextDouble();

        // Sites crowd around the impact, so pieces are small there and large far away (like real broken glass).
        Vector2 impact = r.min + Vector2.Scale(impactPoint, r.size);
        var sites = new List<Vector2>();
        for (int i = 0; i < credits.Count + debrisPieces; i++)
        {
            var u = new Vector2(r.xMin + Rand() * r.width, r.yMin + Rand() * r.height);
            sites.Add(Vector2.Lerp(impact, u, Mathf.Pow(Rand(), 0.75f)));
        }
        var cells = Voronoi(sites, r);
        cells.Sort((a, b) => Area(b).CompareTo(Area(a)));
#if UNITY_EDITOR
        float total = 0f;
        foreach (var c in cells) total += Area(c);
        Debug.Assert(Mathf.Abs(total - r.width * r.height) < r.width * r.height * 0.001f, "[ShatterCredits] Pieces do not tile the panel.");
#endif
        // Biggest cells carry the credits; debris is split once more into sharper splinters.
        int nCredits = Mathf.Min(credits.Count, cells.Count);
        var debris = new List<Vector2[]>();
        for (int i = nCredits; i < cells.Count; i++)
        {
            var c = cells[i];
            if (c.Length < 4) { debris.Add(c); continue; }
            int h = c.Length / 2;
            // Cut along the diagonal c[0] -> c[h]: both halves stay convex.
            var first = new Vector2[h + 1];
            Array.Copy(c, first, h + 1);
            var second = new Vector2[c.Length - h + 1];
            Array.Copy(c, h, second, 0, c.Length - h);
            second[second.Length - 1] = c[0];
            debris.Add(first);
            debris.Add(second);
        }

        float maxDist = r.size.magnitude;
        Piece Make(Vector2[] poly, string name)
        {
            Vector2 centre = Centroid(poly); // the visual middle: pivot for rotation/zoom and where the text sits
            var local = new Vector2[poly.Length];
            var uv = new Vector2[poly.Length];
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int i = 0; i < poly.Length; i++)
            {
                local[i] = poly[i] - centre;
                min = Vector2.Min(min, local[i]);
                max = Vector2.Max(max, local[i]);
                uv[i] = uvRect.min + Vector2.Scale(Rect.PointToNormalized(r, poly[i]), uvRect.size);
            }
            var g = new GameObject(name, typeof(RectTransform)).AddComponent<GlassShard>();
            g.rectTransform.SetParent(container, false);
            g.rectTransform.sizeDelta = max - min;
            g.Setup(local, uv, uvRect.min + Vector2.Scale(Rect.PointToNormalized(r, centre), uvRect.size), tex);
            // Biggest box centred on the pivot that stays inside the bounding box (the text box).
            Vector2 textBox = 2f * Vector2.Min(-min, max);
            g.raycastTarget = false;
            Vector2 away = centre - impact;
            var p = new Piece
            {
                g = g,
                textBox = textBox,
                home = centre - r.center,
                dir = away.sqrMagnitude > 1f ? away.normalized : Vector2.up,
                depth = Mathf.Lerp(0.3f, 1f, Rand()),
                delay = Mathf.Clamp01(away.magnitude / maxDist),
                spin = (Rand() - 0.5f) * 60f,
            };
            pieces.Add(p);
            return p;
        }

        foreach (var d in debris)
        {
            var p = Make(d, "Debris");
            p.rest = p.home + p.dir * (debrisSpread * Mathf.Lerp(0.3f, 1f, Rand()));
            p.restRot = (Rand() - 0.5f) * 360f;
            p.restScale = Mathf.Lerp(0.5f, 1f, Rand());
            p.depth *= 1.6f; // debris sits "in front", so it drifts more
        }

        // Credit pieces settle on a loose grid inside the panel.
        int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(nCredits * r.width / r.height)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(nCredits / (float)cols));
        var cell = new Vector2(r.width / cols, r.height / rows);
        for (int i = 0; i < nCredits; i++)
        {
            var p = Make(cells[i], "Credit_" + i);
            p.credit = i;
            float size = Mathf.Max(p.g.rectTransform.sizeDelta.x, p.g.rectTransform.sizeDelta.y);
            int row = i / cols, col = i % cols;
            p.rest = new Vector2((col + 0.5f) * cell.x - r.width * 0.5f, r.height * 0.5f - (row + 0.5f) * cell.y)
                     + Vector2.Scale(new Vector2(Rand() - 0.5f, Rand() - 0.5f), cell * 0.25f);
            p.restRot = (Rand() - 0.5f) * 20f;
            p.restScale = Mathf.Min(cell.x, cell.y) * 0.85f / size;
            p.zoomScale = Mathf.Min(r.width, r.height) * zoomSize / size;

            var g = p.g;
            g.raycastTarget = true;
            g.Hovered = (_, on) => p.hovered = on;
            g.Clicked = (_, e) => OnPieceClicked(p, e);
            creditPieces.Add(p);

            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(g.rectTransform, false);
            text.rectTransform.sizeDelta = p.textBox * 0.75f; // centred on the piece's middle, roughly inside the polygon
            if (font != null) text.font = font;
            text.text = $"<b>{credits[i].title}</b>\n<size=70%>{credits[i].body}";
            text.enableAutoSizing = true;
            text.fontSizeMin = 6f;
            text.fontSizeMax = 40f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.alpha = 0f;
            p.text = text;
        }
        container.gameObject.SetActive(false);
    }

    /// <summary>Crops the texture like "cover": fills the rect without stretching.</summary>
    static Rect CoverUV(Texture tex, Rect r)
    {
        if (tex == null || r.width <= 0f || r.height <= 0f) return new Rect(0f, 0f, 1f, 1f);
        float texAspect = tex.width / (float)tex.height, rectAspect = r.width / r.height;
        if (texAspect > rectAspect) { float w = rectAspect / texAspect; return new Rect((1f - w) * 0.5f, 0f, w, 1f); }
        float h = texAspect / rectAspect;
        return new Rect(0f, (1f - h) * 0.5f, 1f, h);
    }

    /// <summary>Voronoi cells of the sites, clipped to the rect: each cell is the rect cut by every bisector.</summary>
    public static List<Vector2[]> Voronoi(IList<Vector2> sites, Rect bounds)
    {
        var cells = new List<Vector2[]>();
        var poly = new List<Vector2>();
        var next = new List<Vector2>();
        for (int i = 0; i < sites.Count; i++)
        {
            poly.Clear();
            poly.Add(new Vector2(bounds.xMin, bounds.yMin));
            poly.Add(new Vector2(bounds.xMax, bounds.yMin));
            poly.Add(new Vector2(bounds.xMax, bounds.yMax));
            poly.Add(new Vector2(bounds.xMin, bounds.yMax));
            for (int j = 0; j < sites.Count && poly.Count >= 3; j++)
            {
                Vector2 n = sites[j] - sites[i];
                if (j == i || n.sqrMagnitude < 1e-6f) continue;
                float d = Vector2.Dot((sites[i] + sites[j]) * 0.5f, n);
                next.Clear();
                for (int k = 0; k < poly.Count; k++)
                {
                    Vector2 a = poly[k], b = poly[(k + 1) % poly.Count];
                    float da = Vector2.Dot(a, n) - d, db = Vector2.Dot(b, n) - d;
                    if (da <= 0f) next.Add(a);
                    if (da * db < 0f) next.Add(a + (b - a) * (da / (da - db)));
                }
                (poly, next) = (next, poly);
            }
            if (poly.Count >= 3) cells.Add(poly.ToArray());
        }
        return cells;
    }

    /// <summary>Area centroid of a convex polygon (unlike the vertex average, not pulled toward clusters of corners).</summary>
    static Vector2 Centroid(Vector2[] p)
    {
        Vector2 c = Vector2.zero;
        float a = 0f;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 u = p[i], v = p[(i + 1) % p.Length];
            float cross = u.x * v.y - v.x * u.y;
            a += cross;
            c += (u + v) * cross;
        }
        return Mathf.Abs(a) < 1e-4f ? p[0] : c / (3f * a);
    }

    static float Area(Vector2[] p)
    {
        float a = 0f;
        for (int i = 0; i < p.Length; i++) a += p[i].x * p[(i + 1) % p.Length].y - p[(i + 1) % p.Length].x * p[i].y;
        return Mathf.Abs(a) * 0.5f;
    }

    #endregion
}
