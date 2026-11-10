using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A shutter door that slides up/down and can ONLY be operated from a SecurityConsole (no [E] on the door itself).
///   Toggle : each console use flips it open/closed and it stays that way.
///   Timed  : a console use opens it; it closes again after Open Time. A countdown is shown above the door
///            and pauses during Tetra's time stop.
/// Link a console by dragging this door into the console's list, or give both the same Channel Id.
/// Walking up to the door still shows its status popup (open/closed + time left).
/// </summary>
public class SecurityDoor_Manager : DoorBase
{
    public enum DoorType { Toggle, Timed }

    /// <summary>Every security door in the loaded scenes.</summary>
    public static readonly List<SecurityDoor_Manager> All = new List<SecurityDoor_Manager>();

    [Header("Security Door")]
    [SerializeField] DoorType doorType = DoorType.Toggle;
    [Tooltip("Consoles with the same Channel Id control this door (optional, you can also link directly on the console).")]
    [SerializeField] string channelId = "";
    [Tooltip("Display name in popups, e.g. 'Door A'.")]
    [SerializeField] string displayName = "Security Door";
    [Tooltip("The part that slides up. Its solid collider moves with it.")]
    [SerializeField] Transform panel;
    [Tooltip("How far the panel slides up. 0 = the solid collider's height.")]
    [SerializeField] float slideHeight = 0f;
    [Tooltip("Optional light tinted green (open) / red (closed).")]
    [SerializeField] SpriteRenderer stateLight;

    [Header("Timed")]
    [Tooltip("Countdown pauses during Tetra's time stop.")]
    [SerializeField] bool pauseTimerDuringTimeStop = true;
    [Tooltip("Countdown text above the door. Created automatically if empty.")]
    [SerializeField] TMP_Text countdownText;
    [SerializeField] Vector2 countdownOffset = new Vector2(0f, 0.5f);

    [Header("Security Door Events")]
    public UnityEvent<float> onTimerTick;      // seconds left
    public UnityEvent onTimerWarning;          // countdown reached the warning time
    public UnityEvent onTimerPaused;
    public UnityEvent onTimerResumed;

    public DoorType Type => doorType;
    public string ChannelId => channelId;
    public string DisplayName => displayName;
    public float TimeLeft { get; private set; }
    public bool TimerPaused => pauseTimerDuringTimeStop && TimeStop_Manager.IsTimeStopped;

    float closedY;
    float height;
    Tween slideTween, countdownPulse;
    bool warned;
    bool wasPaused;
    int lastShownSecond = -1;

    static readonly Color OpenColor = new Color(0.4f, 1f, 0.5f);
    static readonly Color ClosedColor = new Color(1f, 0.35f, 0.3f);

    protected override void Awake()
    {
        if (panel == null) panel = solidCollider != null ? solidCollider.transform : transform;
        closedY = panel.localPosition.y;
        height = slideHeight > 0f ? slideHeight
            : solidCollider != null ? solidCollider.bounds.size.y / Mathf.Max(Mathf.Abs(panel.lossyScale.y), 0.0001f) * Mathf.Abs(panel.localScale.y)
            : 2f;
        base.Awake();
        if (doorType == DoorType.Timed) EnsureCountdownText();
    }

    void OnEnable() => All.Add(this);

    protected override void OnDisable()
    {
        base.OnDisable();
        All.Remove(this);
    }

    void OnDestroy()
    {
        slideTween?.Kill();
        countdownPulse?.Kill();
    }

    protected override void Update()
    {
        base.Update();
        if (doorType != DoorType.Timed || !IsOpen) return;

        bool paused = TimerPaused;
        if (paused != wasPaused)
        {
            wasPaused = paused;
            if (paused) onTimerPaused?.Invoke();
            else onTimerResumed?.Invoke();
        }

        if (!paused && TimeLeft > 0f)
        {
            TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
            onTimerTick?.Invoke(TimeLeft);
            if (!warned && TimeLeft <= profile.warningTime)
            {
                warned = true;
                onTimerWarning?.Invoke();
                StartWarningPulse();
            }
        }

        if (TimeLeft <= 0f) SetOpen(false); // retries every frame while the doorway is blocked

        UpdateCountdownText(paused);
    }

    #region Console API

    /// <summary>Called by a SecurityConsole.</summary>
    public void ActivateFromConsole()
    {
        if (doorType == DoorType.Toggle)
        {
            Toggle();
            return;
        }

        // Timed: open (or restart the timer if already open)
        TimeLeft = profile.openTime;
        warned = false;
        countdownPulse?.Kill();
        if (countdownText != null) countdownText.transform.localScale = Vector3.one;
        if (!IsOpen) Open();
        else PunchPopup();
    }

    public bool IsControlledBy(string channel) => !string.IsNullOrEmpty(channel) && channel == channelId;

    #endregion

    public override bool CanInteract(PlayerCharacter character) => false; // consoles only
    protected override bool OnInteract(PlayerCharacter character) => false;

    protected override void ApplyState(bool open, bool instant)
    {
        float y = open ? closedY + height : closedY;
        slideTween?.Kill();
        if (instant) panel.localPosition = new Vector3(panel.localPosition.x, y, panel.localPosition.z);
        else slideTween = panel.DOLocalMoveY(y, profile.slideDuration)
            .SetEase(open ? profile.slideOpenEase : profile.slideCloseEase).SetLink(gameObject);

        if (stateLight != null) stateLight.DOColor(open ? OpenColor : ClosedColor, 0.2f).SetLink(gameObject);

        if (!open)
        {
            TimeLeft = 0f;
            countdownPulse?.Kill();
        }
        if (countdownText != null) countdownText.gameObject.SetActive(open && doorType == DoorType.Timed);
        lastShownSecond = -1;
    }

    protected override string GetStatusText(PlayerCharacter character)
    {
        string state = StateText;
        if (doorType == DoorType.Timed && IsOpen)
            state += TimerPaused ? $" <size=80%>{Colored("(time stopped)", new Color(0.6f, 0.85f, 1f))}</size>" : $" <size=80%>{TimeLeft:0.0}s</size>";
        string mode = doorType == DoorType.Timed ? "timed" : "toggle";
        return $"{displayName}: {state}\n<size=70%>Use a console ({mode})</size>";
    }

    #region Countdown

    void EnsureCountdownText()
    {
        if (countdownText != null) return;
        var go = new GameObject("Countdown");
        go.transform.SetParent(transform, false);
        float top = solidCollider != null ? solidCollider.bounds.max.y - transform.position.y : 2f;
        go.transform.localPosition = new Vector3(countdownOffset.x, top + countdownOffset.y, 0f);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize = 4f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(3f, 1.2f);
        tmp.sortingOrder = 150;
        countdownText = tmp;
        go.SetActive(false);
    }

    void UpdateCountdownText(bool paused)
    {
        if (countdownText == null) return;
        int shown = Mathf.CeilToInt(TimeLeft);
        countdownText.color = paused ? new Color(0.6f, 0.85f, 1f) : TimeLeft <= profile.warningTime ? ClosedColor : Color.white;
        string text = paused ? $"{shown} ||" : shown.ToString();
        if (countdownText.text != text) countdownText.text = text;

        if (shown != lastShownSecond && !paused)
        {
            lastShownSecond = shown;
            if (!warned) countdownText.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 4, 0.5f).SetLink(gameObject);
        }
    }

    void StartWarningPulse()
    {
        if (countdownText == null) return;
        countdownPulse?.Kill();
        countdownText.transform.localScale = Vector3.one;
        countdownPulse = countdownText.transform.DOScale(1.3f, 0.25f).SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
    }

    #endregion
}
