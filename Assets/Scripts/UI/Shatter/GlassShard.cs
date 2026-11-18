using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One glass piece: a convex polygon showing its part of the artwork. Made by ShatterCredits at runtime.
/// The mouse only hits the polygon itself, not its bounding box.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class GlassShard : MaskableGraphic, ICanvasRaycastFilter, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action<GlassShard, bool> Hovered;
    public Action<GlassShard, PointerEventData> Clicked;

    Vector2[] points; // local to the pivot (the area centroid, always inside a convex polygon)
    Vector2[] uvs;
    Vector2 uvCentre;
    Texture texture;
    float edgeGlow;

    public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

    /// <summary>0 = plain art (pieces look seamless), 1 = bright glass edges.</summary>
    public float EdgeGlow
    {
        get => edgeGlow;
        set { if (Mathf.Approximately(edgeGlow, value)) return; edgeGlow = value; SetVerticesDirty(); }
    }

    public void Setup(Vector2[] localPoints, Vector2[] uv, Vector2 centreUV, Texture tex)
    {
        points = localPoints;
        uvs = uv;
        uvCentre = centreUV;
        texture = tex;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Length < 3) return;
        Color centre = color;
        Color rim = Color.Lerp(color, new Color(1f, 1f, 1f, color.a), edgeGlow);
        // Fan from the centre so the rim can be brighter, like light catching a glass edge.
        vh.AddVert(Vector2.zero, centre, uvCentre);
        for (int i = 0; i < points.Length; i++) vh.AddVert(points[i], rim, uvs[i]);
        for (int i = 0; i < points.Length; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % points.Length);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (points == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var p)) return false;
        float sign = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Length];
            float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            if (cross == 0f) continue;
            if (sign == 0f) sign = Mathf.Sign(cross);
            else if (Mathf.Sign(cross) != sign) return false;
        }
        return true;
    }

    public void OnPointerEnter(PointerEventData e) => Hovered?.Invoke(this, true);
    public void OnPointerExit(PointerEventData e) => Hovered?.Invoke(this, false);
    public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(this, e);
}
