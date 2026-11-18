using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

// Imported from DuckyGame (item ScriptableObject dependency replaced by the Interactable checkbox).
// UI sticker that the player can grab anywhere and pull off. Inspired by: https://github.com/CatsJuice/sticker-forge
// Setup: this script sits on the PARENT which has an Image.
// A child GameObject (e.g. "StickerIMG") holds the visible sticker Image that gets peeled.
// Dragging lifts, tilts and curls the sticker; past the peel distance it tears off and only the glue residue stays behind. Add a StickerCurlEffect to the StickerIMG for the curl.

[RequireComponent(typeof(Image))]
public class StickerPeelManager : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
{
    [Header("Interaction")]
    [Tooltip("Untick to lock the sticker (e.g. until a menu animation finishes).")]
    [SerializeField] private bool interactable = true;

    [Header("Sticker Visual (child that gets peeled off)")]
    [SerializeField] private Image stickerIMG;

    [Header("Glue Residue")]
    [Tooltip("Does the designer want a glue residue left behind after peeling?")]
    [SerializeField] private bool useGlueResidue = true;
    [Tooltip("Material for the residue look. If empty, the Image keeps whatever material it already has (e.g. StickerResidue_MAT).")]
    [SerializeField] private Material residueMaterial;

    [Header("Peel Feel")]
    [Tooltip("FALLBACK drag distance (canvas units) to tear off - only used when the sticker has NO StickerCurlEffect. With a curl effect, the distance is derived from the sticker's own size instead.")]
    [SerializeField] private float peelDistance = 150f;
    [Tooltip("With a StickerCurlEffect: how many sticker-lengths the player must drag to fully peel. 1 = one sticker-length (fast), 2 = two sticker-lengths (the fold tip tracks the cursor roughly 1:1). ")]
    [SerializeField] private float peelSpanMultiplier = 2f;
    [Tooltip("How much the sticker scales up while being pulled (simulates lifting toward the camera).")]
    [SerializeField] private float maxLiftScale = 1.08f;
    [Tooltip("Maximum tilt in degrees while being pulled.")]
    [SerializeField] private float maxTiltDegrees = 12f;
    [Tooltip("How quickly tilt / curl ease toward their targets. Higher = snappier, lower = floatier.")]
    [SerializeField] private float smoothing = 12f;
    [SerializeField] private float snapBackDuration = 0.25f;
    [SerializeField] private float flyOffDuration = 0.35f;

    [Header("Peel Hint (needs a StickerCurlEffect)")]
    [Tooltip("At rest, one corner lifts a little now and then to show the sticker can be dragged off. Untick to reuse the sticker without the hint.")]
    [SerializeField] private bool showPeelHint = true;
    [Tooltip("The corner that lifts.")]
    [SerializeField] private HintCorner hintCorner = HintCorner.TopRight;
    [Tooltip("Seconds of rest between two corner lifts.")]
    [SerializeField] private float hintInterval = 2f;
    [Tooltip("Seconds one corner takes to lift and settle back.")]
    [SerializeField] private float hintDuration = 0.6f;
    [Tooltip("How far the corner lifts, as a fraction of a full peel.")]
    [Range(0.01f, 0.3f)] [SerializeField] private float hintLift = 0.08f;

    [Header("Events")]
    [SerializeField] private UnityEvent onClickSticker;
    [SerializeField] private UnityEvent onFinishPeeling;

    private Image residueImage;
    private RectTransform stickerRT;
    private StickerCurlEffect curlEffect;   // optional, found on stickerIMG
    private Vector2 stickerStartAnchoredPos;
    private Vector2 grabScreenPos;
    private Vector2 dragDirection = Vector2.right;
    private float canvasScale = 1f;

    // Target values set by drag; displayed values chase them each frame for smooth motion.
    private float targetProgress;
    private float displayedProgress;
    private float targetTilt;
    private float displayedTilt;
    private float progressVelocity, tiltVelocity;

    private readonly System.Collections.Generic.List<Graphic> fadingChildren = new System.Collections.Generic.List<Graphic>();

    private bool isDragging;
    private bool peeled;

    public enum HintCorner { TopRight, TopLeft, BottomLeft, BottomRight }
    private static readonly Vector2[] HintCorners = { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) };
    private float hintClock;
    private bool hintBent;

    private void Start()
    {
        residueImage = GetComponent<Image>();

        if (stickerIMG == null)
        {
            Debug.LogWarning($"[{name}] StickerPeelManager: Sticker IMG is not assigned.", this);
            enabled = false;
            return;
        }

        stickerRT = stickerIMG.rectTransform;
        stickerStartAnchoredPos = stickerRT.anchoredPosition;
        curlEffect = stickerIMG.GetComponent<StickerCurlEffect>();
        foreach (Graphic g in stickerIMG.GetComponentsInChildren<Graphic>(true))
            if (g != stickerIMG) fadingChildren.Add(g);

        if (useGlueResidue)
        {
            // Keep both images in sync - the child's sprite is the source of truth, in case someone accidentally changed the parent's.
            //Basically idiotproof
            residueImage.sprite = stickerIMG.sprite;
            if (residueMaterial != null) residueImage.material = residueMaterial;
        }
        else
        {
            // In case the effect needs to be applied on newspaper or non-glue surfaces, the residue image can be disabled entirely.
            residueImage.enabled = false;
        }
    }

    private void Update()
    {
        if (peeled) return;
        // At rest: only the hint animates (and it rebuilds the curl mesh only while a corner is lifting).
        if (!isDragging && targetProgress == 0f && displayedProgress < 0.0001f && Mathf.Abs(displayedTilt) < 0.01f)
        {
            UpdateHint();
            return;
        }

        // Ease displayed values toward their targets with a critically damped spring (like sticker-forge): it accelerates and settles
        // smoothly, instead of jumping to full speed the moment the pointer moves.
        float smoothTime = 1f / Mathf.Max(0.01f, smoothing);
        displayedTilt = Mathf.SmoothDampAngle(displayedTilt, targetTilt, ref tiltVelocity, smoothTime);
        displayedProgress = Mathf.Clamp01(Mathf.SmoothDamp(displayedProgress, targetProgress, ref progressVelocity, smoothTime));

        stickerRT.localRotation = Quaternion.Euler(0f, 0f, displayedTilt);
        stickerRT.localScale = Vector3.one * Mathf.Lerp(1f, maxLiftScale, displayedProgress);
        curlEffect?.SetCurl(displayedProgress, dragDirection);
        foreach (Graphic g in fadingChildren) g.canvasRenderer.SetAlpha(1f - displayedProgress);
    }

    // Lifts the chosen corner: wait hintInterval, then up and back down over hintDuration, repeat.
    private void UpdateHint()
    {
        if (curlEffect == null) return;
        if (!showPeelHint || !interactable)
        {
            if (hintBent) FlattenHint();
            hintClock = 0f;
            return;
        }

        hintClock += Time.deltaTime;
        float phase = (hintClock - hintInterval) / Mathf.Max(0.01f, hintDuration);
        if (phase < 0f) return;
        if (phase >= 1f)
        {
            FlattenHint();
            hintClock = 0f;
            return;
        }

        // The curl folds the side opposite the direction first, so point it from the corner toward the centre (along the diagonal).
        Vector2 dir = -Vector2.Scale(HintCorners[(int)hintCorner], stickerRT.rect.size).normalized;
        curlEffect.SetCurl(hintLift * Mathf.Sin(phase * Mathf.PI), dir);
        hintBent = true;
    }

    private void FlattenHint()
    {
        curlEffect.SetCurl(0f, dragDirection);
        hintBent = false;
    }

    //Pointer handlers (clicks on the child bubble up to this parent)

    public void OnPointerDown(PointerEventData e)
    {
        if (!CanPeel()) return;

        grabScreenPos = e.position;
        isDragging = true;
        hintClock = 0f; // the drag takes over the curl; the hint restarts its wait after the sticker settles
        hintBent = false;
        // Read per drag: the scale changes when the browser window resizes / goes fullscreen.
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasScale = canvas.scaleFactor;
        onClickSticker?.Invoke();

        // Cancel any snap-back tween still running from a previous failed peel to avoid Tween conflicts
        stickerRT.DOKill();
    }

    // A click without movement never gets OnEndDrag, so release the drag state here (a real drag is ended by OnEndDrag).
    public void OnPointerUp(PointerEventData e)
    {
        if (!e.dragging) isDragging = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!CanPeel() || !isDragging) return;

        // Drag vector in canvas units (screen pixels divided by canvas scale).
        Vector2 dragDelta = (e.position - grabScreenPos) / canvasScale;
        if (dragDelta.sqrMagnitude > 0.0001f) dragDirection = dragDelta.normalized;

        // With a curl effect the required distance comes from the sticker's own size, so the tear-off moment always matches the visual "fully folded" state.
        // Without one, fall back to the fixed peelDistance value.
        float requiredDrag = curlEffect != null
            ? curlEffect.GetPeelSpan(dragDirection) * peelSpanMultiplier
            : peelDistance;

        targetProgress = Mathf.Clamp01(dragDelta.magnitude / requiredDrag);
        targetTilt = -Mathf.Sign(dragDelta.x) * targetProgress * maxTiltDegrees;

        // Position follows the pointer directly (only tilt/curl are smoothed - position lag under the cursor feels unresponsive).
        stickerRT.anchoredPosition = stickerStartAnchoredPos + dragDelta;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (peeled || !isDragging) return; // not CanPeel(): a sticker locked mid-drag must still snap back
        isDragging = false;

        if (targetProgress >= 1f && interactable)
        {
            FinishPeel(e.position);
        }
        else
        {
            // Not far enough - snap the sticker back onto the residue.
            // Tilt and curl ease back via Update; position and scale tween via DOTween.
            targetProgress = 0f;
            targetTilt = 0f;
            stickerRT.DOAnchorPos(stickerStartAnchoredPos, snapBackDuration).SetEase(Ease.OutBack);
        }
    }

    /// <summary>Lock / unlock peeling from code or a UnityEvent.</summary>
    public void SetInteractable(bool value) => interactable = value;

    private bool CanPeel()
    {
        if (peeled) return false;
        return interactable;
    }

    private void FinishPeel(Vector2 releaseScreenPos)
    {
        peeled = true;

        // Fly off: keep travelling away from the residue in the direction of the pull, then vanish.
        Vector2 flyDirection = ((releaseScreenPos - grabScreenPos) / canvasScale).normalized;
        Vector2 flyTarget = stickerRT.anchoredPosition + flyDirection * peelDistance;

        Sequence peelSequence = DOTween.Sequence();
        peelSequence.Append(stickerRT.DOAnchorPos(flyTarget, flyOffDuration).SetEase(Ease.OutQuad));
        peelSequence.Join(stickerIMG.DOFade(0f, flyOffDuration));

        // Finish the curl during the fly-off so the sticker visibly completes its fold as it leaves (Update stops driving the curl once 'peeled' is set, so no conflict here).
        if (curlEffect != null)
        {
            peelSequence.Join(DOTween.To(
                () => displayedProgress,
                x => { displayedProgress = x; curlEffect.SetCurl(x, dragDirection); },
                1f, flyOffDuration));
        }

        peelSequence.OnComplete(() =>
        {
            stickerIMG.gameObject.SetActive(false);
            onFinishPeeling?.Invoke();
        });
    }
}
