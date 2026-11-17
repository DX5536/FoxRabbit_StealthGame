using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// Bends a UI Image like a sticker being peeled. Subdivides the Image quad into a grid and folds every vertex past a "peel line" over to the other side,
// tinted to suggest the sticker's backside. Drive it from code via SetCurl(progress, direction).
// Attach to the SAME GameObject as the sticker Image (e.g. StickerIMG) aka ChildObject!!!
[RequireComponent(typeof(Image))]
public class StickerCurlEffect : BaseMeshEffect
{
    [Header("Mesh")]
    [Tooltip("Subdivisions per side. More = smoother fold line, slightly more vertices.")]
    [Range(2, 32)]
    [SerializeField] private int gridSize = 12;

    [Header("Look")]
    [Tooltip("Tint multiplied over the folded-back part (stickers show a dimmer underside).")]
    [SerializeField] private Color backsideTint = new Color(0.82f, 0.82f, 0.82f, 1f);

    // Set from StickerPeelManager each frame.
    private float curlProgress;              // 0 = flat, 1 = fully folded over
    private Vector2 curlDirection = Vector2.right;

    // Update the curl state. Direction = the drag direction in screen/canvas space.</summary>
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

        RectTransform rt = graphic.rectTransform;
        Rect rect = rt.rect;
        Color baseColor = graphic.color;

        // Sprite UVs (works for simple sprites; sliced/tiled not supported).
        Image img = graphic as Image;
        Vector4 uv = (img != null && img.overrideSprite != null)
            ? DataUtility.GetOuterUV(img.overrideSprite)
            : new Vector4(0f, 0f, 1f, 1f);

        /* FOLD LOGIC/MATH
        The crease line is perpendicular to the drag direction and sweeps the FULL span of the sticker as progress goes 0 -> 1. 
        At progress 1 the crease has passed the leading edge, so EVERY vertex is folded - no flat wedges can remain, even when the
        player drags diagonally (a mirrored rectangle half never lines up with the other half on diagonal creases, so stopping at the center leaves uncovered corners).*/

        Vector2 dir = curlDirection;
        Vector2 center = rect.center;
        float maxExtent = 0.5f * (Mathf.Abs(dir.x) * rect.width + Mathf.Abs(dir.y) * rect.height);
        float lineProj = maxExtent - curlProgress * 2f * maxExtent;

        int vertsPerRow = gridSize + 1;
        Vector3[] positions = new Vector3[vertsPerRow * vertsPerRow];
        Color32[] colors = new Color32[positions.Length];
        Vector2[] uvs = new Vector2[positions.Length];
        bool[] folded = new bool[positions.Length];

        for (int row = 0; row <= gridSize; row++)
        {
            for (int col = 0; col <= gridSize; col++)
            {
                int i = row * vertsPerRow + col;
                float tx = col / (float)gridSize;
                float ty = row / (float)gridSize;

                Vector2 pos = new Vector2(rect.xMin + tx * rect.width, rect.yMin + ty * rect.height);
                uvs[i] = new Vector2(Mathf.Lerp(uv.x, uv.z, tx), Mathf.Lerp(uv.y, uv.w, ty));

                // Signed distance behind the peel line (positive = should be folded).
                float proj = Vector2.Dot(pos - center, -dir);
                float d = proj - lineProj;

                if (curlProgress > 0f && d > 0f)
                {
                    // Mirror across the peel line -> the trailing part flips over the sticker.
                    pos += dir * (2f * d);
                    folded[i] = true;
                    colors[i] = baseColor * backsideTint;
                }
                else
                {
                    folded[i] = false;
                    colors[i] = baseColor;
                }
                positions[i] = pos;
            }
        }

        vh.Clear();
        for (int i = 0; i < positions.Length; i++)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = positions[i];
            v.color = colors[i];
            v.uv0 = uvs[i];
            vh.AddVert(v);
        }

        // Two passes so the folded part draws ON TOP of the flat part.
        AddQuads(vh, folded, vertsPerRow, addFoldedQuads: false);
        AddQuads(vh, folded, vertsPerRow, addFoldedQuads: true);
    }

    private void AddQuads(VertexHelper vh, bool[] folded, int vertsPerRow, bool addFoldedQuads)
    {
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                int i = row * vertsPerRow + col;
                bool quadIsFolded = folded[i] || folded[i + 1] || folded[i + vertsPerRow] || folded[i + vertsPerRow + 1];
                if (quadIsFolded != addFoldedQuads) continue;

                vh.AddTriangle(i, i + vertsPerRow, i + vertsPerRow + 1);
                vh.AddTriangle(i, i + vertsPerRow + 1, i + 1);
            }
        }
    }
}
