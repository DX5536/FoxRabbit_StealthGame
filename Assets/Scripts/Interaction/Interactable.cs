using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base for anything a character can use with [E] (doors, consoles...).
/// Needs a trigger Collider2D on the same GameObject as the interaction zone (added automatically if missing).
/// While the selected character stands in the zone, a small status bubble pops up above it
/// (e.g. "[E] Open" / "CLOSED") - an accessibility hint that also tells the player what [E] will do.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Higher priority wins when several interactables are in range.")]
    [SerializeField] int priority = 0;
    [Tooltip("Size of the interaction zone if no trigger collider exists yet.")]
    [SerializeField] Vector2 defaultZoneSize = new Vector2(2.5f, 2.5f);

    [Header("Status Popup")]
    [SerializeField] bool showStatusPopup = true;
    [Tooltip("Popup position relative to this object.")]
    [SerializeField] Vector2 popupOffset = new Vector2(0f, 2.6f);
    [SerializeField] float popupFontSize = 2.6f;
    [SerializeField] int popupSortingOrder = 200;

    [Header("Interaction Events")]
    public UnityEvent onInteract;
    public UnityEvent onCharacterEnter;
    public UnityEvent onCharacterExit;

    public int Priority => priority;
    public virtual Vector2 InteractPoint => transform.position;

    readonly HashSet<PlayerCharacter> inside = new HashSet<PlayerCharacter>();
    Transform popup;
    TextMeshPro popupText;
    SpriteRenderer popupBackground;
    Tween popupTween;
    bool popupVisible;

    protected virtual void Awake()
    {
        var zone = GetComponent<Collider2D>();
        if (zone == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = defaultZoneSize;
            zone = box;
        }
        zone.isTrigger = true;
    }

    protected virtual void Update()
    {
        if (!showStatusPopup) return;
        PlayerCharacter active = PlayerController.Instance != null ? PlayerController.Instance.ActiveCharacter : null;
        bool show = active != null && inside.Contains(active) && !GameOver_Manager.IsGameOver;
        if (show != popupVisible) SetPopupVisible(show);
        if (popupVisible) RefreshPopup(active);
    }

    protected virtual void OnDisable()
    {
        foreach (var c in inside) if (c != null) c.RemoveInteractable(this);
        inside.Clear();
        if (popup != null) popup.gameObject.SetActive(false);
        popupVisible = false;
    }

    #region API for subclasses

    public virtual bool CanInteract(PlayerCharacter character) => isActiveAndEnabled;

    /// <summary>Called by PlayerCharacter on [E]. Return true if something happened.</summary>
    public bool Interact(PlayerCharacter character)
    {
        if (!CanInteract(character)) return false;
        bool done = OnInteract(character);
        if (done)
        {
            onInteract?.Invoke();
            PunchPopup();
        }
        return done;
    }

    protected abstract bool OnInteract(PlayerCharacter character);

    /// <summary>Text shown in the popup (rich text allowed).</summary>
    protected abstract string GetStatusText(PlayerCharacter character);

    /// <summary>Re-pop the status bubble (call after a state change).</summary>
    protected void PunchPopup()
    {
        if (!popupVisible || popup == null) return;
        popupTween?.Kill();
        popup.localScale = Vector3.one;
        popupTween = popup.DOPunchScale(Vector3.one * 0.2f, 0.25f, 6, 0.6f).SetLink(gameObject);
    }

    protected static string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    #endregion

    #region Trigger

    static PlayerCharacter GetCharacter(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        return rb != null ? rb.GetComponent<PlayerCharacter>() : null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var character = GetCharacter(other);
        if (character == null || !inside.Add(character)) return;
        character.AddInteractable(this);
        onCharacterEnter?.Invoke();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var character = GetCharacter(other);
        if (character == null || !inside.Remove(character)) return;
        character.RemoveInteractable(this);
        onCharacterExit?.Invoke();
    }

    #endregion

    #region Popup

    void SetPopupVisible(bool visible)
    {
        popupVisible = visible;
        if (visible && popup == null) BuildPopup();
        if (popup == null) return;

        popupTween?.Kill();
        if (visible)
        {
            popup.gameObject.SetActive(true);
            popup.localScale = Vector3.zero;
            popupTween = popup.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(gameObject);
        }
        else
        {
            popupTween = popup.DOScale(0f, 0.15f).SetEase(Ease.InBack).SetLink(gameObject)
                .OnComplete(() => popup.gameObject.SetActive(false));
        }
    }

    void RefreshPopup(PlayerCharacter character)
    {
        string text = GetStatusText(character);
        if (popupText.text == text) return;
        popupText.text = text;
        popupText.ForceMeshUpdate();
        Vector2 size = popupText.GetRenderedValues(false);
        popupBackground.transform.localScale = new Vector3(size.x + 0.5f, size.y + 0.3f, 1f);
    }

    void BuildPopup()
    {
        popup = new GameObject("StatusPopup").transform;
        popup.SetParent(transform, false);
        popup.localPosition = popupOffset;

        var bg = new GameObject("Background");
        bg.transform.SetParent(popup, false);
        popupBackground = bg.AddComponent<SpriteRenderer>();
        popupBackground.sprite = RuntimeAssets.WhiteSprite;
        popupBackground.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
        popupBackground.sortingOrder = popupSortingOrder;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(popup, false);
        popupText = textGo.AddComponent<TextMeshPro>();
        popupText.fontSize = popupFontSize;
        popupText.alignment = TextAlignmentOptions.Center;
        popupText.textWrappingMode = TextWrappingModes.NoWrap;
        popupText.richText = true;
        popupText.color = Color.white;
        popupText.rectTransform.sizeDelta = new Vector2(8f, 2f);
        popupText.sortingOrder = popupSortingOrder + 1;
    }

    #endregion
}
