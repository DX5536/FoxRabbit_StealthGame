using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class TestSceneBuilder
{
    /// <summary>
    /// Placeholder "peel to start" sticker (StickerPeelManager on the residue parent, StickerCurlEffect on the child).
    /// Not hooked to StartGame yet: add MenuController.StartGame to its On Finish Peeling event when ready.
    /// </summary>
    static void BuildStartSticker(Transform parent)
    {
        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // rounded rect placeholder
        var residueMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StickerResidue_MAT.mat");

        // Parent = glue residue left behind
        var residue = UIPanel("StartSticker", parent, new Vector2(560, -60), new Vector2(380, 220), Color.white);
        residue.sprite = sprite;
        residue.type = Image.Type.Simple;

        // Child = the sticker that gets peeled
        var sticker = UIPanel("StickerIMG", residue.transform, Vector2.zero, new Vector2(380, 220), new Color(1f, 0.82f, 0.3f));
        sticker.sprite = sprite;
        sticker.type = Image.Type.Simple; // the curl mesh only supports Simple images
        sticker.gameObject.AddComponent<StickerCurlEffect>();
        var label = UIText("Label", sticker.transform, "START", Vector2.zero, new Vector2(360, 200), 72);
        label.color = new Color(0.25f, 0.15f, 0.05f);
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;

        UIText("PeelHint", parent, "peel the sticker to start", new Vector2(560, -200), new Vector2(500, 50), 28).raycastTarget = false;

        var peel = residue.gameObject.AddComponent<StickerPeelManager>();
        Set(peel, "stickerIMG", sticker);
        Set(peel, "useGlueResidue", true);
        if (residueMat != null) Set(peel, "residueMaterial", residueMat);
        else Debug.LogWarning("[TestSceneBuilder] Assets/Materials/StickerResidue_MAT.mat not found - residue keeps the default UI material.");
        // Values tuned in DuckyGame
        Set(peel, "peelDistance", 1000f);
        Set(peel, "peelSpanMultiplier", 0.5f);
        Set(peel, "maxLiftScale", 1.08f);
        Set(peel, "maxTiltDegrees", 15f);
        Set(peel, "smoothing", 5f);
        Set(peel, "snapBackDuration", 0.5f);
        Set(peel, "flyOffDuration", 0.3f);
    }
}
