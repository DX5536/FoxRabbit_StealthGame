using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene changes with a short fade to black. SceneLoader.Load(buildIndex) from anywhere.
/// The fade overlay is created on first use and survives scene loads.
/// </summary>
public static class SceneLoader
{
    const float FadeTime = 0.35f;
    static CanvasGroup overlay;
    static bool loading;

    public static void Load(int buildIndex)
    {
        if (loading) return;
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"[SceneLoader] Build index {buildIndex} is not in the build list (File > Build Profiles).");
            return;
        }

        loading = true;
        EnsureOverlay();
        overlay.blocksRaycasts = true;
        overlay.DOFade(1f, FadeTime).SetUpdate(true).OnComplete(() =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(buildIndex);
            overlay.DOFade(0f, FadeTime).SetUpdate(true).SetDelay(0.05f).OnComplete(() =>
            {
                overlay.blocksRaycasts = false;
                loading = false;
            });
        });
    }

    static void EnsureOverlay()
    {
        if (overlay != null) return;
        var go = new GameObject("SceneFade");
        Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        go.AddComponent<GraphicRaycaster>();
        overlay = go.AddComponent<CanvasGroup>();
        overlay.alpha = 0f;

        var img = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        overlay = null;
        loading = false;
    }
}
