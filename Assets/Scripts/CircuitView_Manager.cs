using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hold [Shift] (configurable) to see circuit lines from every SecurityConsole to the doors it controls,
/// drawn like a circuit trace (up from the console, along a bus, down to the door) in the console's Circuit Color.
/// Lines draw in from the console when the key is pressed and fade out on release.
/// One per scene is enough.
/// </summary>
public class CircuitView_Manager : MonoBehaviour
{
    [Tooltip("Hold to show the circuit. Note: Left Shift is also sprint - pick another key if that gets in the way.")]
    [SerializeField] Key showKey = Key.LeftShift;
    [Tooltip("Gamepad button that also shows the circuit (none = disabled).")]
    [SerializeField] bool gamepadSelectButton = true;

    [Header("Look")]
    [SerializeField] float lineWidth = 0.08f;
    [Tooltip("Height of the horizontal 'bus' above the higher end point.")]
    [SerializeField] float busHeight = 1.2f;
    [Tooltip("Extra height per line so parallel lines don't overlap.")]
    [SerializeField] float busSpacing = 0.3f;
    [SerializeField] float drawDuration = 0.45f;
    [SerializeField] float fadeDuration = 0.2f;
    [Tooltip("Size of the dots at both ends.")]
    [SerializeField] float endDotSize = 0.25f;
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] int sortingOrder = 180;
    [Tooltip("Optional material. Empty = URP Sprite-Unlit-Default.")]
    [SerializeField] Material lineMaterial;

    public bool IsShowing { get; private set; }

    class Link
    {
        public LineRenderer line;
        public SpriteRenderer startDot, endDot;
        public Vector3[] path;
        public float[] cumulative;
        public Color color;
    }

    readonly List<Link> links = new List<Link>();
    readonly List<Link> pool = new List<Link>();
    float progress;   // 0..1 draw-in
    float alpha;      // 0..1 visibility
    Tween progressTween, alphaTween;
    static Sprite dotSprite;

    void Update()
    {
        bool held = false;
        bool inputAllowed = !GameOver_Manager.IsGameOver && (PlayerController.Instance == null || PlayerController.Instance.InputEnabled);
        if (inputAllowed)
        {
            var kb = Keyboard.current;
            if (kb != null && kb[showKey].isPressed) held = true;
            var pad = Gamepad.current;
            if (gamepadSelectButton && pad != null && pad.selectButton.isPressed) held = true;
        }

        if (held && !IsShowing) Show();
        else if (!held && IsShowing) Hide();

        if (alpha > 0f) RefreshLines();
    }

    void OnDisable()
    {
        progressTween?.Kill();
        alphaTween?.Kill();
        foreach (var l in links) SetLinkActive(l, false);
    }

    void Show()
    {
        IsShowing = true;
        BuildLinks();
        progressTween?.Kill();
        alphaTween?.Kill();
        progress = 0f;
        alpha = 1f;
        progressTween = DOTween.To(() => progress, v => progress = v, 1f, drawDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
    }

    void Hide()
    {
        IsShowing = false;
        alphaTween?.Kill();
        alphaTween = DOTween.To(() => alpha, v => alpha = v, 0f, fadeDuration).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { foreach (var l in links) SetLinkActive(l, false); });
    }

    void BuildLinks()
    {
        foreach (var l in links) { SetLinkActive(l, false); pool.Add(l); }
        links.Clear();

        int index = 0;
        foreach (var console in SecurityConsole.All)
        {
            if (console == null) continue;
            foreach (var door in console.GetDoors())
            {
                if (door == null) continue;
                Link link = pool.Count > 0 ? pool[pool.Count - 1] : CreateLink();
                if (pool.Count > 0) pool.RemoveAt(pool.Count - 1);
                link.color = console.CircuitColor;
                link.path = BuildPath(console.InteractPoint, door.InteractPoint, index++);
                link.cumulative = Cumulative(link.path);
                SetLinkActive(link, true);
                links.Add(link);
            }
        }
    }

    Vector3[] BuildPath(Vector2 from, Vector2 to, int index)
    {
        float busY = Mathf.Max(from.y, to.y) + busHeight + index * busSpacing;
        return new[]
        {
            new Vector3(from.x, from.y, 0f),
            new Vector3(from.x, busY, 0f),
            new Vector3(to.x, busY, 0f),
            new Vector3(to.x, to.y, 0f)
        };
    }

    static float[] Cumulative(Vector3[] path)
    {
        var c = new float[path.Length];
        for (int i = 1; i < path.Length; i++) c[i] = c[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        return c;
    }

    void RefreshLines()
    {
        foreach (var link in links)
        {
            float total = link.cumulative[link.cumulative.Length - 1];
            float target = total * progress;

            // Count how many path points are revealed, then add the partial end point.
            int count = 1;
            while (count < link.path.Length && link.cumulative[count] <= target) count++;
            var points = new Vector3[Mathf.Min(count + 1, link.path.Length)];
            for (int i = 0; i < count && i < link.path.Length; i++) points[i] = link.path[i];
            if (count < link.path.Length)
            {
                float segLen = link.cumulative[count] - link.cumulative[count - 1];
                float t = segLen > 0f ? (target - link.cumulative[count - 1]) / segLen : 1f;
                points[count] = Vector3.Lerp(link.path[count - 1], link.path[count], t);
            }

            link.line.positionCount = points.Length;
            link.line.SetPositions(points);

            // Gentle pulse so it reads as "live" power.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            Color c = new Color(link.color.r, link.color.g, link.color.b, link.color.a * alpha * pulse);
            link.line.startColor = link.line.endColor = c;

            link.startDot.transform.position = link.path[0];
            link.startDot.color = c;
            link.endDot.transform.position = link.path[link.path.Length - 1];
            link.endDot.color = new Color(c.r, c.g, c.b, c.a * (progress >= 0.99f ? 1f : 0f));
        }
    }

    Link CreateLink()
    {
        var go = new GameObject("CircuitLine");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = lineWidth;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.sharedMaterial = GetMaterial();
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;

        return new Link { line = lr, startDot = CreateDot(go.transform), endDot = CreateDot(go.transform) };
    }

    SpriteRenderer CreateDot(Transform parent)
    {
        if (dotSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            dotSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
        var go = new GameObject("Dot");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * endDotSize;
        go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dotSprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder + 1;
        return sr;
    }

    Material GetMaterial()
    {
        if (lineMaterial != null) return lineMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        lineMaterial = new Material(shader) { name = "CircuitLine (Runtime)" };
        return lineMaterial;
    }

    static void SetLinkActive(Link link, bool active)
    {
        if (link?.line != null) link.line.gameObject.SetActive(active);
    }
}
