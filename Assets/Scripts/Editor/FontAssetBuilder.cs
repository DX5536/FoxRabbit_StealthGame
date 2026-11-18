using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Turns every .ttf/.otf in Assets/Font into a TextMeshPro font asset ("<name> SDF.asset") next to it.
/// Runs once automatically after the fonts are imported; Tools > FoxRabbit > Create TMP Fonts runs it again.
/// Dynamic atlas: glyphs are added when first used, so every character works without picking a character set.
/// </summary>
public static class FontAssetBuilder
{
    const string Folder = "Assets/Font";

    [InitializeOnLoadMethod]
    static void AutoCreate() => EditorApplication.delayCall += () => Create(false);

    [MenuItem("Tools/FoxRabbit/Create TMP Fonts")]
    static void CreateFromMenu() => Create(true);

    static void Create(bool log)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) return;
        int created = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { Folder }))
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(guid);
            string assetPath = $"{Folder}/{Path.GetFileNameWithoutExtension(fontPath)} SDF.asset";
            if (File.Exists(assetPath)) continue;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            if (fa == null) { Debug.LogError($"[FontAssetBuilder] Could not create a font asset from {fontPath}."); continue; }

            fa.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            // The atlas texture and material must live inside the asset, or they are lost on reload.
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            created++;
        }
        if (created > 0 || log)
        {
            SetMainFont();
            AssetDatabase.SaveAssets();
            Debug.Log($"[FontAssetBuilder] Created {created} TextMeshPro font asset(s) in {Folder}. Main font: {MainFont}.");
        }
    }

    const string MainFont = Folder + "/NotoSerif-Regular SDF.asset";
    const string MainItalic = Folder + "/NotoSerif-Italic SDF.asset";

    /// <summary>Makes Noto Serif the TMP default (new text uses it) and lets &lt;i&gt; use the real italic.</summary>
    static void SetMainFont()
    {
        var main = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFont);
        if (main == null || TMP_Settings.instance == null) return;
        var italic = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainItalic);
        if (italic != null && main.fontWeightTable.Length > 4)
        {
            main.fontWeightTable[4].italicTypeface = italic; // index 4 = Regular (400)
            EditorUtility.SetDirty(main);
        }
        var settings = new SerializedObject(TMP_Settings.instance);
        settings.FindProperty("m_defaultFontAsset").objectReferenceValue = main;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
