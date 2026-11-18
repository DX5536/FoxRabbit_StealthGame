using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// Bends a UI Image like a sticker being peeled. Subdivides the Image quad into a grid; everything past a "peel line"
// rolls over a small cylinder (like sticker-forge) and lands face-down on the other side, tinted to suggest the backside.
// Drive it from code via SetCurl(progress, direction).
// Attach to the SAME GameObject as the sticker Image (e.g. StickerIMG) aka ChildObject!!!
[RequireComponent(typeof(Image))]
public class StickerCurlEffect : BaseMeshEffect
{
    [Header("Mesh")]
    [Tooltip("Subdivisions per side. More = rounder curl, slightly more vertices.")]
    [Range(2, 48)]
    [SerializeField] private int gridSize = 24;

    [Header("Look")]
    [Tooltip("Tint multiplied over the folded-back part (stickers show a dimmer underside).")]
    [SerializeField] private Color backsideTint = new Color(0.82f, 0.82f, 0.82f, 1f);
    [Tooltip("Radius of the rolled edge, as a fraction of the sticker's short side. 0 = sharp crease.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float curlRadius = 0.12f;
    [Tooltip("How much the rolled edge darkens where it turns away from the viewer.")]
    [Range(0f, 1f)]
    [SerializeField] private float curlShading = 0.35f;

    // Set from StickerPeelManager each frame.
    private float curlProgress;              // 0 = flat, 1 = fully folded over
    private Vector2 curlDirection = Vector2.right;

    // Update the curl state. Direction = the drag direction in screen/canvas space.
    public void SetCurl(float progress, Vector2 direction)
    {
        curlProgress = Mathf.Clamp01(progress);
        if (direction.sqrMagnitude > 0.0001f) curlDirection = direction.normalized;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    // The sticker's full length along the given drag direction, in local rect units.
    // StickerPeelManager uses this to derive how far the player must drag to peel fully, so the tear threshold automatically matches the sticker's size.
    public float GetPeelSpan(Vector2 direction)
    {
        if (graphic == null) return 0f;
        Rect rect = graphic.rectTransform.rect;
        Vector2 d = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        return Mathf.Abs(d.x) * rect.width + Mathf.Abs(d.y) * rect.height;
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || graphic == null) return;

        Rect rect = graphic.rectTransform.rect;
        Color baseColor = graphic.color;

        // Sprite UVs (works for simple sprites; sliced/tiled not supported).
        Image img = graphic as Image;
        Vector4 uv = (img != null && img.overrideSprite != null)
            ? DataUtility.GetOuterUV(img.overrideSprite)
            : new Vector4(0f, 0f, 1f, 1f);

        /* FOLD LOGIC/MATH
        The crease line is perpendicular to the drag direction and sweeps the FULL span of the sticker as progress goes 0 -> 1.
        At progress 1 the crease has passed the leading edge, so EVERY vertex is folded - no flat wedges can remain, even when the
        player drags diagonally (a mirrored rectangle half never lines up with the other half on diagonal creases, so stopping at the center leaves uncovered corners).
        A point d behind the crease wraps around a cylinder of radius r: angle = d / r. Up to 90 degrees it is the front, tilting away;
        past 90 it is the backside coming over the top; past 180 it lies flat, face down. Seen from above, it sits r * sin(angle) past the crease. */

        Vector2 dir = curlDirection;
        Vector2 center = rect.center;
        float maxExtent = 0.5f * (Mathf.Abs(dir.x) * rect.width + Mathf.Abs(dir.y) * rect.height);
        float lineProj = maxExtent - curlProgress * 2f * maxExtent;
        // Small peels (the hint) get a tighter roll, so the corner visibly turns over instead of barely bulging.
        float depth = curlProgress * 2f * maxExtent;
        float radius = Mathf.Max(0.001f, Mathf.Min(curlRadius * Mathf.Min(rect.width, rect.height), depth / (0.75f * Mathf.PI)));

        vh.Clear();
        UIVertex Corner(int col, int row)
        {
            float tx = col / (float)gridSize, ty = row / (float)gridSize;
            UIVertex v = UIVertex.simpleVert;
            v.position = new Vector2(rect.xMin + tx * rect.width, rect.yMin + ty * rect.height);
            v.uv0 = new Vector2(Mathf.Lerp(uv.x, uv.z, tx), Mathf.Lerp(uv.y, uv.w, ty));
            v.color = baseColor;
            return v;
        }
        // Signed distance behind the peel line, on the flat sticker (positive = rolled part).
        Func<UIVertex, float> Behind = v => Vector2.Dot((Vector2)v.position - center, -dir) - lineProj; // one allocation per rebuild

        // Every grid triangle is CUT at the peel line and at the top of the roll (90 degrees), then drawn in three layers:
        // flat part, front tilting away, backside on top. Whole grid squares straddling a line would tear the mesh.
        float top = radius * Mathf.PI * 0.5f;
        int layers = curlProgress > 0f ? 3 : 1;
        for (int layer = 0; layer < layers; layer++)
        {
            float lo = layer == 0 ? float.NegativeInfinity : layer == 1 ? 0f : top;
            float hi = layer == 0 ? 0f : layer == 1 ? top : float.PositiveInfinity;
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    UIVertex a = Corner(col, row), b = Corner(col, row + 1), c = Corner(col + 1, row + 1), d = Corner(col + 1, row);
                    EmitClipped(vh, a, b, c, lo, hi, Behind, dir, radius);
                    EmitClipped(vh, a, c, d, lo, hi, Behind, dir, radius);
                }
            }
        }
    }

    private static readonly List<UIVertex> polyA = new List<UIVertex>(8);
    private static readonly List<UIVertex> polyB = new List<UIVertex>(8);

    // Keeps the part of triangle abc whose distance behind the peel line is in [lo, hi], rolls it over the cylinder and adds it as a fan.
    private void EmitClipped(VertexHelper vh, UIVertex a, UIVertex b, UIVertex c, float lo, float hi, Func<UIVertex, float> behind, Vector2 dir, float radius)
    {
        polyA.Clear(); polyA.Add(a); polyA.Add(b); polyA.Add(c);
        if (!float.IsNegativeInfinity(lo)) { Clip(polyA, polyB, behind, -1f, lo); CopyBack(); }
        if (!float.IsPositiveInfinity(hi)) { Clip(polyA, polyB, behind, 1f, hi); CopyBack(); }
        if (polyA.Count < 3) return;

        int start = vh.currentVertCount;
        foreach (UIVertex v0 in polyA)
        {
            UIVertex v = v0;
            float d = behind(v);
            if (d > 0f)
            {
                float angle = d / radius;
                float seenPast = angle <= Mathf.PI ? radius * Mathf.Sin(angle) : -(d - Mathf.PI * radius);
                v.position += (Vector3)(-dir * (seenPast - d)); // slide along the peel axis to where it is seen from above
                angle = Mathf.Min(angle, Mathf.PI);
                Color col = (Color)v.color * (1f - curlShading * Mathf.Sin(angle));
                if (angle > Mathf.PI * 0.5f) col *= backsideTint;
                col.a = ((Color)v.color).a;
                v.color = col;
            }
            vh.AddVert(v);
        }
        for (int i = 1; i < polyA.Count - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
    }

    private static void CopyBack()
    {
        polyA.Clear();
        polyA.AddRange(polyB);
    }

    // Sutherland-Hodgman: keeps the part of the polygon where sign * (behind(v) - bound) <= 0
    // (sign +1 keeps behind <= bound, -1 keeps behind >= bound). No lambdas here: this runs thousands of times per rebuild.
    private static void Clip(List<UIVertex> input, List<UIVertex> output, Func<UIVertex, float> behind, float sign, float bound)
    {
        output.Clear();
        for (int k = 0; k < input.Count; k++)
        {
            UIVertex p = input[k], q = input[(k + 1) % input.Count];
            float sp = sign * (behind(p) - bound), sq = sign * (behind(q) - bound);
            if (sp <= 0f) output.Add(p);
            if (sp * sq < 0f) output.Add(Lerp(p, q, sp / (sp - sq)));
        }
    }

    private static UIVertex Lerp(UIVertex p, UIVertex q, float t)
    {
        UIVertex v = p;
        v.position = Vector3.Lerp(p.position, q.position, t);
        v.uv0 = Vector4.Lerp(p.uv0, q.uv0, t);
        v.color = Color32.Lerp(p.color, q.color, t);
        return v;
    }
}
