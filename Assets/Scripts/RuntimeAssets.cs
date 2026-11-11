using UnityEngine;

/// <summary>Tiny shared assets created at runtime (plain white sprite, unlit sprite material).</summary>
public static class RuntimeAssets
{
    static Sprite whiteSprite;
    static Material unlitMaterial;

    /// <summary>1x1 world-unit white sprite, centered pivot.</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                whiteSprite.name = "RuntimeWhite";
            }
            return whiteSprite;
        }
    }

    /// <summary>URP 2D Sprite-Unlit-Default material (vertex colors, no lighting).</summary>
    public static Material UnlitMaterial
    {
        get
        {
            if (unlitMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                unlitMaterial = new Material(shader) { name = "RuntimeUnlit" };
            }
            return unlitMaterial;
        }
    }
}
