using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Game over / level complete popup with a quick restart ([R]).
/// Call GameOver_Manager.TriggerGameOver("reason") from anywhere (EnemyVision_Manager does it automatically).
/// If no popup is assigned, a simple one is built at runtime on a Screen Space - Overlay canvas,
/// so it is never affected by the screen filters.
/// Put one in each level (needed for restart-anytime). If missing, one is created when the game ends.
/// </summary>
[DisallowMultipleComponent]
public class GameOver_Manager : MonoBehaviour
{
    public static GameOver_Manager Instance { get; private set; }
    public static bool IsGameOver { get; private set; }
    /// <summary>Raised once when the game is over (caught or level complete).</summary>
    public static event Action GameOverTriggered;

    [Header("Popup (optional - built automatically if empty)")]
    [SerializeField] CanvasGroup popup;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text messageText;

    [Header("Texts")]
    [SerializeField] string caughtTitle = "SPOTTED!";
    [SerializeField] string winTitle = "LEVEL COMPLETE";
    [SerializeField] string restartHint = "Press [R] to restart";

    [Header("Restart")]
    [SerializeField] Key restartKey = Key.R;
    [Tooltip("Restart with R at any time, not only on the game over screen.")]
    [SerializeField] bool allowRestartAnytime = true;
    [Tooltip("Delay before the popup shows (lets a 'caught' sound/animation play).")]
    [SerializeField] float popupDelay = 0.25f;
    [Tooltip("Slow motion while the popup appears. 1 = off.")]
    [Range(0.05f, 1f)] [SerializeField] float caughtTimeScale = 0.3f;

    [Header("Events")]
    public UnityEvent onCaught;
    public UnityEvent onLevelComplete;
    public UnityEvent onRestart;

    bool restarting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsGameOver = false;
        GameOverTriggered = null;
        Instance = null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        IsGameOver = false;
        Time.timeScale = 1f;
        if (popup == null) BuildPopup();
        popup.alpha = 0f;
        popup.blocksRaycasts = false;
        popup.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb[restartKey].wasPressedThisFrame) return;
        if (IsGameOver || allowRestartAnytime) Restart();
    }

    #region Static API

    public static void TriggerGameOver(string reason = null) => GetOrCreate().ShowEnd(false, reason);

    public static void TriggerLevelComplete(string message = null) => GetOrCreate().ShowEnd(true, message);

    static GameOver_Manager GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject("GameOver_Manager").AddComponent<GameOver_Manager>();
    }

    #endregion

    public void Restart()
    {
        if (restarting) return;
        restarting = true;
        onRestart?.Invoke();
        DOTween.KillAll();
        Time.timeScale = 1f;
        IsGameOver = false;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
#if UNITY_EDITOR
        else UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        else SceneManager.LoadScene(scene.name);
#endif
    }

    void ShowEnd(bool win, string message)
    {
        if (IsGameOver) return;
        IsGameOver = true;

        if (PlayerController.Instance != null) PlayerController.Instance.InputEnabled = false;
        GameOverTriggered?.Invoke();
        if (win) onLevelComplete?.Invoke();
        else onCaught?.Invoke();

        titleText.text = win ? winTitle : caughtTitle;
        titleText.color = win ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.35f, 0.3f);
        messageText.text = string.IsNullOrEmpty(message) ? restartHint : $"{message}\n{restartHint}";

        if (!win) Time.timeScale = caughtTimeScale;

        popup.gameObject.SetActive(true);
        popup.alpha = 0f;
        popup.transform.localScale = Vector3.one * 0.85f;
        DOTween.Sequence()
            .AppendInterval(popupDelay)
            .Append(popup.DOFade(1f, 0.25f))
            .Join(popup.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
            .AppendCallback(() => Time.timeScale = win ? 1f : 0f)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    void BuildPopup()
    {
        var canvasGo = new GameObject("GameOver_Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Popup", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        popup = panel.AddComponent<CanvasGroup>();

        titleText = CreateText(panel.transform, "Title", 110, new Vector2(0f, 60f), FontStyles.Bold);
        messageText = CreateText(panel.transform, "Message", 42, new Vector2(0f, -70f), FontStyles.Normal);
    }

    static TMP_Text CreateText(Transform parent, string name, float size, Vector2 pos, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1600f, 200f);
        rt.anchoredPosition = pos;
        return t;
    }
}
