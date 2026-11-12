using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class TestSceneBuilder
{
    const string CheckpointPrefabPath = "Assets/Prefabs/Checkpoint.prefab";

    /// <summary>Creates Assets/Prefabs/Checkpoint.prefab (placeholder flag art). Place its pivot ~1 unit above the floor.</summary>
    [MenuItem("Tools/FoxRabbit/Create Checkpoint Prefab")]
    public static void CreateCheckpointPrefab()
    {
        if (File.Exists(CheckpointPrefabPath) &&
            !EditorUtility.DisplayDialog("Checkpoint Prefab", $"{CheckpointPrefabPath} exists. Overwrite it?", "Overwrite", "Cancel"))
            return;
        SaveCheckpointPrefab();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath));
        Debug.Log($"[TestSceneBuilder] Created {CheckpointPrefabPath}. Drag it into a level.");
    }

    /// <summary>Places a checkpoint (prefab instance, created if missing) with its pivot at pos.</summary>
    internal static void PlaceCheckpoint(Transform parent, Vector2 pos)
    {
        if (!File.Exists(CheckpointPrefabPath)) SaveCheckpointPrefab();
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath));
        go.transform.SetParent(parent);
        go.transform.position = pos;
    }

    static void SaveCheckpointPrefab()
    {
        Directory.CreateDirectory(AssetFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(CheckpointPrefabPath));
        square = CreateSquareSprite();

        var root = new GameObject("Checkpoint");
        var zone = root.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = new Vector2(1.5f, 3f);

        Block(root.transform, "Pole", new Vector2(-0.4f, 0f), new Vector2(0.1f, 2f), new Color(0.8f, 0.8f, 0.85f));
        var flag = Block(root.transform, "Flag", new Vector2(-0.05f, 0.75f), new Vector2(0.6f, 0.4f), Color.white);

        var checkpoint = root.AddComponent<Checkpoint>();
        Set(checkpoint, "flag", flag);

        PrefabUtility.SaveAsPrefabAsset(root, CheckpointPrefabPath);
        Object.DestroyImmediate(root);
    }

    static SpriteRenderer Block(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = 3;
        return sr;
    }
}
