using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tetra's ability: stop time.
///   Charging : the hourglass fills over Cooldown Duration.
///   Ready    : the hourglass hums / glows. [F] (with Tetra selected) starts the time stop.
///   Active   : TimeStopFilter_Manager is on and the hourglass drains over Time Stop Duration.
///   Then it is empty again and refills.
/// Gameplay that should freeze (enemies, projectiles...) can read TimeStop_Manager.IsTimeStopped
/// or subscribe to TimeStop_Manager.TimeStopChanged / the UnityEvents below.
/// Assign this component to Tetra's PlayerCharacter.ability.
/// </summary>
public class TimeStop_Manager : CharacterAbility
{
    public enum State { Charging, Ready, Active }

    /// <summary>True while any time stop is active.</summary>
    public static bool IsTimeStopped { get; private set; }
    /// <summary>Raised with true when time stops and false when it resumes.</summary>
    public static event Action<bool> TimeStopChanged;

    [Header("Time Stop")]
    [Tooltip("Empty = TimeStopFilter_Manager.Instance.")]
    [SerializeField] TimeStopFilter_Manager filter;
    [Min(0.1f)] [SerializeField] float timeStopDuration = 5f;
    [Tooltip("How long the hourglass takes to fill up.")]
    [Min(0.1f)] [SerializeField] float cooldownDuration = 10f;
    [SerializeField] bool startReady = false;
    [Tooltip("Keep recharging while the other character is selected. Turn off if it's too strong.")]
    [SerializeField] bool rechargeWhileNotSelected = true;
    [Tooltip("Unscaled = keeps running when Time.timeScale is 0 (pause menu). Usually leave off.")]
    [SerializeField] bool useUnscaledTime = false;

    [Header("Hourglass UI")]
    [SerializeField] HourglassFill hourglass;
    [SerializeField] Ease fillEase = Ease.Linear;
    [Tooltip("Hourglass drains during the time stop to show the time left.")]
    [SerializeField] bool drainDuringTimeStop = true;

    [Header("Ready Glow (humming)")]
    [Tooltip("What hums when ready. Empty = the hourglass.")]
    [SerializeField] Transform glowTarget;
    [SerializeField] float humScale = 1.08f;
    [Tooltip("Seconds for one hum (grow + shrink is twice this).")]
    [SerializeField] float humPeriod = 0.6f;
    [Tooltip("Optional object enabled while ready (e.g. a glow sprite).")]
    [SerializeField] GameObject readyGlowObject;
    [Tooltip("Optional renderer whose material float is pulsed while ready (e.g. AllIn1 '_Glow'). Enable the effect on the material first.")]
    [SerializeField] Renderer glowRenderer;
    [SerializeField] string glowProperty = "_Glow";
    [SerializeField] float glowMin = 0f;
    [SerializeField] float glowMax = 5f;

    [Header("Time Stop Events")]
    public UnityEvent onCooldownStarted;
    public UnityEvent onReady;
    public UnityEvent onTimeStopStarted;
    public UnityEvent onTimeStopEnded;
    public UnityEvent<float> onFillChanged;

    public State CurrentState { get; private set; } = State.Charging;
    public float FillAmount { get; private set; }
    public float CooldownDuration { get => cooldownDuration; set => cooldownDuration = Mathf.Max(0.1f, value); }
    public float TimeStopDuration { get => timeStopDuration; set => timeStopDuration = Mathf.Max(0.1f, value); }
    public bool RechargeWhileNotSelected { get => rechargeWhileNotSelected; set => rechargeWhileNotSelected = value; }

    Tween fillTween;
    Tween humTween, glowTween;
    Vector3 glowBaseScale = Vector3.one;
    Material glowMaterial;
    int glowPropertyId;

    void Start()
    {
        if (filter == null) filter = TimeStopFilter_Manager.Instance;
        if (glowTarget == null && hourglass != null) glowTarget = hourglass.transform;
        if (glowTarget != null) glowBaseScale = glowTarget.localScale;
        if (readyGlowObject != null) readyGlowObject.SetActive(false);

        if (glowRenderer != null && !string.IsNullOrEmpty(glowProperty))
        {
            glowPropertyId = Shader.PropertyToID(glowProperty);
            glowMaterial = glowRenderer.material; // instance, so other sprites using the material are not affected
            if (!glowMaterial.HasProperty(glowPropertyId)) glowMaterial = null;
            else glowMaterial.SetFloat(glowPropertyId, glowMin);
        }

        if (startReady) BecomeReady(false);
        else StartCharging();
    }

    void Update()
    {
        // Pause / resume the recharge depending on selection (setting can change at runtime).
        if (CurrentState == State.Charging && fillTween != null && fillTween.IsActive())
        {
            bool shouldRun = rechargeWhileNotSelected || IsSelected;
            if (shouldRun && !fillTween.IsPlaying()) fillTween.Play();
            else if (!shouldRun && fillTween.IsPlaying()) fillTween.Pause();
            if (hourglass != null) hourglass.StreamActive = shouldRun;
        }
    }

    void OnDisable()
    {
        if (CurrentState == State.Active) EndTimeStop(false);
        KillTweens();
    }

    public override bool CanActivate() => base.CanActivate() && CurrentState == State.Ready;

    protected override bool Activate()
    {
        CurrentState = State.Active;
        StopGlow();

        if (filter == null) filter = TimeStopFilter_Manager.Instance;
        if (filter != null) filter.Activate();
        else Debug.LogWarning("[TimeStop_Manager] No TimeStopFilter_Manager found, only gameplay time stop is triggered.", this);

        SetTimeStopped(true);
        onTimeStopStarted?.Invoke();

        fillTween?.Kill();
        if (hourglass != null) hourglass.StreamActive = false;
        if (drainDuringTimeStop)
        {
            fillTween = DOTween.To(() => FillAmount, SetFill, 0f, timeStopDuration)
                .SetEase(Ease.Linear).SetUpdate(useUnscaledTime).SetLink(gameObject)
                .OnComplete(() => EndTimeStop(true));
        }
        else
        {
            fillTween = DOVirtual.DelayedCall(timeStopDuration, () => EndTimeStop(true), useUnscaledTime).SetLink(gameObject);
        }
        return true;
    }

    #region Public API

    /// <summary>End the time stop early (e.g. Tetra got hit).</summary>
    public void CancelTimeStop()
    {
        if (CurrentState == State.Active) EndTimeStop(true);
    }

    /// <summary>Skip the cooldown (debug / pickups).</summary>
    public void RefillInstant()
    {
        if (CurrentState == State.Charging) BecomeReady(true);
    }

    #endregion

    void StartCharging()
    {
        CurrentState = State.Charging;
        StopGlow();
        SetFill(0f);
        if (hourglass != null) hourglass.StreamActive = true;
        onCooldownStarted?.Invoke();

        fillTween?.Kill();
        fillTween = DOTween.To(() => FillAmount, SetFill, 1f, cooldownDuration)
            .SetEase(fillEase).SetUpdate(useUnscaledTime).SetLink(gameObject)
            .OnComplete(() => BecomeReady(true));
        if (!rechargeWhileNotSelected && !IsSelected) fillTween.Pause();
    }

    void BecomeReady(bool pop)
    {
        fillTween?.Kill();
        CurrentState = State.Ready;
        SetFill(1f);
        StartGlow(pop);
        onReady?.Invoke();
    }

    void EndTimeStop(bool startRecharge)
    {
        fillTween?.Kill();
        if (filter != null) filter.Deactivate();
        SetTimeStopped(false);
        onTimeStopEnded?.Invoke();
        if (startRecharge) StartCharging();
        else CurrentState = State.Charging;
    }

    void SetFill(float value)
    {
        FillAmount = value;
        if (hourglass != null) hourglass.SetFill(value);
        onFillChanged?.Invoke(value);
    }

    void StartGlow(bool pop)
    {
        StopGlow();
        if (readyGlowObject != null) readyGlowObject.SetActive(true);

        if (glowTarget != null)
        {
            var seq = DOTween.Sequence().SetLink(gameObject).SetUpdate(useUnscaledTime);
            if (pop) seq.Append(glowTarget.DOPunchScale(glowBaseScale * 0.35f, 0.35f, 8, 0.6f));
            seq.Append(glowTarget.DOScale(glowBaseScale * humScale, humPeriod).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
            humTween = seq;
        }

        if (glowMaterial != null)
        {
            glowTween = DOTween.To(() => glowMaterial.GetFloat(glowPropertyId), v => glowMaterial.SetFloat(glowPropertyId, v), glowMax, humPeriod)
                .From(glowMin).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(useUnscaledTime).SetLink(gameObject);
        }
    }

    void StopGlow()
    {
        humTween?.Kill();
        glowTween?.Kill();
        if (glowTarget != null) glowTarget.localScale = glowBaseScale;
        if (glowMaterial != null) glowMaterial.SetFloat(glowPropertyId, glowMin);
        if (readyGlowObject != null) readyGlowObject.SetActive(false);
    }

    void KillTweens()
    {
        fillTween?.Kill();
        StopGlow();
    }

    static void SetTimeStopped(bool value)
    {
        if (IsTimeStopped == value) return;
        IsTimeStopped = value;
        TimeStopChanged?.Invoke(value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsTimeStopped = false;
        TimeStopChanged = null;
    }
}
