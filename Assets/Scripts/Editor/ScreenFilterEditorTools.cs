using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor helpers for TimeStopFilter_Manager and GlassBreakFilter.</summary>
public static class ScreenFilterEditorTools
{
    static readonly string[] Tags = { "Tetra", "IgnoreTimeStop" };
    const string IgnoreLayer = "IgnoreTimeStop";

    [MenuItem("Tools/Screen Filters/Create TimeStop Tags & Layer")]
    public static void CreateTagsAndLayer()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[ScreenFilters] Could not open TagManager.asset.");
            return;
        }

        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        SerializedProperty layersProp = tagManager.FindProperty("layers");

        foreach (string tag in Tags)
        {
            bool exists = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) { exists = true; break; }

            if (!exists)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
                Debug.Log($"[ScreenFilters] Added tag '{tag}'.");
            }
        }

        bool layerExists = false;
        for (int i = 0; i < layersProp.arraySize; i++)
            if (layersProp.GetArrayElementAtIndex(i).stringValue == IgnoreLayer) { layerExists = true; break; }

        if (!layerExists)
        {
            bool added = false;
            for (int i = 6; i < layersProp.arraySize; i++) // 0-5 are Unity built-in layers
            {
                var layer = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = IgnoreLayer;
                    Debug.Log($"[ScreenFilters] Added layer '{IgnoreLayer}' at index {i}.");
                    added = true;
                    break;
                }
            }
            if (!added) Debug.LogError("[ScreenFilters] No free layer slot for 'IgnoreTimeStop'.");
        }

        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}

/// <summary>
/// Builds a direction map (as in the article) from a diffuse crack texture:
/// every shard between the cracks gets one direction pointing away from the impact point,
/// so the shards "shoot out" from the middle. The shard containing the impact point (the hole) does not move.
/// </summary>
public class GlassDirectionMapGenerator : EditorWindow
{
    Texture2D source;
    float crackThreshold = 0.4f;
    Vector2 impactPoint = new Vector2(0.5f, 0.5f);
    float strength = 1f;
    float distanceFalloff = 0.5f;
    float angleJitter = 12f;
    int minShardPixels = 200;
    float maxShardFraction = 0.2f;
    int seed = 1234;
    Texture2D preview;

    [MenuItem("Tools/Screen Filters/Glass Direction Map Generator")]
    static void Open() => GetWindow<GlassDirectionMapGenerator>("Glass Direction Map");

    void OnEnable()
    {
        if (source == null)
        {
            string[] guids = AssetDatabase.FindAssets("GlassBreak_1920x1080px_Shader t:Texture2D");
            if (guids.Length > 0) source = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Generates <texture>_DirectionMap.png next to the crack texture. Each shard is pushed away from the impact point.", MessageType.Info);

        source = (Texture2D)EditorGUILayout.ObjectField("Crack Texture", source, typeof(Texture2D), false);
        crackThreshold = EditorGUILayout.Slider(new GUIContent("Crack Threshold", "Pixels brighter than this count as crack lines (shard borders)."), crackThreshold, 0.05f, 0.95f);
        impactPoint = EditorGUILayout.Vector2Field(new GUIContent("Impact Point (0..1)", "Where the shards shoot out from."), impactPoint);
        strength = EditorGUILayout.Slider("Strength", strength, 0f, 1f);
        distanceFalloff = EditorGUILayout.Slider(new GUIContent("Distance Falloff", "0 = all shards move equally, 1 = far shards move much more than near ones."), distanceFalloff, 0f, 1f);
        angleJitter = EditorGUILayout.Slider(new GUIContent("Angle Jitter (deg)", "Random rotation per shard so it looks less perfect."), angleJitter, 0f, 45f);
        minShardPixels = EditorGUILayout.IntField(new GUIContent("Min Shard Pixels", "Smaller regions do not move."), minShardPixels);
        maxShardFraction = EditorGUILayout.Slider(new GUIContent("Max Shard Size", "Regions larger than this fraction of the image (the central hole) do not move."), maxShardFraction, 0.01f, 1f);
        seed = EditorGUILayout.IntField("Random Seed", seed);

        using (new EditorGUI.DisabledScope(source == null))
        {
            if (GUILayout.Button("Generate", GUILayout.Height(28))) Generate();
        }

        if (preview != null)
        {
            Rect r = GUILayoutUtility.GetAspectRect((float)preview.width / preview.height);
            EditorGUI.DrawPreviewTexture(r, preview);
        }
    }

    void Generate()
    {
        string srcPath = AssetDatabase.GetAssetPath(source);
        string fullPath = Path.GetFullPath(srcPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[ScreenFilters] Cannot read '{srcPath}'.");
            return;
        }

        // Read the original file so the texture does not need Read/Write enabled.
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(fullPath));
        int w = tex.width, h = tex.height, n = w * h;
        Color32[] src = tex.GetPixels32();
        DestroyImmediate(tex);

        EditorUtility.DisplayProgressBar("Glass Direction Map", "Finding shards...", 0.1f);
        try
        {
            // 1. Crack mask
            byte thr = (byte)(crackThreshold * 255f);
            var isCrack = new bool[n];
            for (int i = 0; i < n; i++)
            {
                Color32 c = src[i];
                int lum = (c.r * 77 + c.g * 150 + c.b * 29) >> 8;
                isCrack[i] = lum * c.a / 255 > thr;
            }

            // 2. Label shards (4-connected flood fill over non-crack pixels)
            var label = new int[n];
            for (int i = 0; i < n; i++) label[i] = -1;
            var areas = new List<int>();
            var sumX = new List<double>();
            var sumY = new List<double>();
            var queue = new int[n];

            for (int start = 0; start < n; start++)
            {
                if (isCrack[start] || label[start] >= 0) continue;
                int id = areas.Count;
                int head = 0, tail = 0;
                queue[tail++] = start;
                label[start] = id;
                int area = 0;
                double sx = 0, sy = 0;

                while (head < tail)
                {
                    int p = queue[head++];
                    int x = p % w, y = p / w;
                    area++; sx += x; sy += y;

                    if (x > 0) TryPush(p - 1);
                    if (x < w - 1) TryPush(p + 1);
                    if (y > 0) TryPush(p - w);
                    if (y < h - 1) TryPush(p + w);
                }

                areas.Add(area);
                sumX.Add(sx);
                sumY.Add(sy);

                void TryPush(int q)
                {
                    if (isCrack[q] || label[q] >= 0) return;
                    label[q] = id;
                    queue[tail++] = q;
                }
            }

            if (areas.Count == 0)
            {
                Debug.LogError("[ScreenFilters] No shards found. Lower the crack threshold.");
                return;
            }

            // 3. Give crack pixels the label of the nearest shard (multi-source BFS) so there are no gaps
            EditorUtility.DisplayProgressBar("Glass Direction Map", "Filling cracks...", 0.5f);
            {
                int head = 0, tail = 0;
                for (int i = 0; i < n; i++) if (label[i] >= 0) queue[tail++] = i;
                while (head < tail)
                {
                    int p = queue[head++];
                    int x = p % w, y = p / w;
                    int l = label[p];
                    if (x > 0 && label[p - 1] < 0) { label[p - 1] = l; queue[tail++] = p - 1; }
                    if (x < w - 1 && label[p + 1] < 0) { label[p + 1] = l; queue[tail++] = p + 1; }
                    if (y > 0 && label[p - w] < 0) { label[p - w] = l; queue[tail++] = p - w; }
                    if (y < h - 1 && label[p + w] < 0) { label[p + w] = l; queue[tail++] = p + w; }
                }
            }

            // 4. Direction per shard: away from the impact point
            EditorUtility.DisplayProgressBar("Glass Direction Map", "Assigning directions...", 0.8f);
            float cx = Mathf.Clamp01(impactPoint.x) * (w - 1);
            float cy = Mathf.Clamp01(impactPoint.y) * (h - 1); // Texture2D rows start at the bottom, same as UV
            int cxi = Mathf.RoundToInt(cx), cyi = Mathf.RoundToInt(cy);
            int holeLabel = label[cyi * w + cxi];
            float maxDist = Mathf.Sqrt(Mathf.Max(cx, w - cx) * Mathf.Max(cx, w - cx) + Mathf.Max(cy, h - cy) * Mathf.Max(cy, h - cy));
            int maxArea = Mathf.RoundToInt(maxShardFraction * n);

            var rng = new System.Random(seed);
            var dirs = new Vector2[areas.Count];
            for (int id = 0; id < areas.Count; id++)
            {
                if (id == holeLabel || areas[id] < minShardPixels || areas[id] > maxArea)
                {
                    dirs[id] = Vector2.zero;
                    continue;
                }

                var centroid = new Vector2((float)(sumX[id] / areas[id]), (float)(sumY[id] / areas[id]));
                Vector2 d = centroid - new Vector2(cx, cy);
                float dist = d.magnitude;
                if (dist < 0.001f) { dirs[id] = Vector2.zero; continue; }

                float angle = ((float)rng.NextDouble() * 2f - 1f) * angleJitter * Mathf.Deg2Rad;
                d /= dist;
                d = new Vector2(d.x * Mathf.Cos(angle) - d.y * Mathf.Sin(angle), d.x * Mathf.Sin(angle) + d.y * Mathf.Cos(angle));

                float mag = strength * Mathf.Lerp(1f, Mathf.Clamp01(dist / maxDist), distanceFalloff);
                dirs[id] = d * mag;
            }

            // 5. Encode like the article: color = InverseLerp(-1, 1, direction)
            var outPixels = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = label[i] >= 0 ? dirs[label[i]] : Vector2.zero;
                outPixels[i] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.InverseLerp(-1f, 1f, d.x) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.InverseLerp(-1f, 1f, d.y) * 255f),
                    0, 255);
            }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            outTex.SetPixels32(outPixels);
            outTex.Apply();

            string outPath = Path.Combine(Path.GetDirectoryName(srcPath) ?? "Assets", Path.GetFileNameWithoutExtension(srcPath) + "_DirectionMap.png").Replace('\\', '/');
            File.WriteAllBytes(Path.GetFullPath(outPath), outTex.EncodeToPNG());
            DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(outPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;              // data, not color
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;    // keep hard shard edges
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.SaveAndReimport();
            }

            preview = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            AssignToSceneFilter(preview);

            int moving = 0;
            foreach (var d in dirs) if (d != Vector2.zero) moving++;
            Debug.Log($"[ScreenFilters] Direction map saved to {outPath} ({areas.Count} regions, {moving} moving shards).", preview);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    void AssignToSceneFilter(Texture2D map)
    {
        var filter = Object.FindFirstObjectByType<GlassBreakFilter>();
        if (filter == null || map == null) return;

        var so = new SerializedObject(filter);
        so.FindProperty("directionMap").objectReferenceValue = map;
        var crack = so.FindProperty("crackTexture");
        if (crack.objectReferenceValue == null) crack.objectReferenceValue = source;
        so.ApplyModifiedProperties();
        Debug.Log($"[ScreenFilters] Assigned direction map to '{filter.name}'.", filter);
    }
}
