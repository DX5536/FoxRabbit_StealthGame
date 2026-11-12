using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// Visual novel intro on top of a level, driven by a Yarn script.
/// Plays when the level starts (not again after a checkpoint restart or a death), locks player input
/// while it runs, then fades the stage out and gameplay begins.
///
/// Yarn commands (all names case-insensitive):
///   <<show Tetra happy left>>   character id (VNCharacter.id), expression, left | center | right
///   <<hide Tetra>>              <<hide_all>>
///   <<bg lab>>                  background sprite by name (Backgrounds list)     <<bg_clear>>
///   <<bgm intro>>  <<stop_bgm>>  <<sfx door_open>>   names from the SoundLibrary
///   <<load_scene 2>>            fade to another scene (build index)
///   <<wait 1>>                  built into Yarn
/// </summary>
public class VisualNovel_Manager : MonoBehaviour
{
    [SerializeField] DialogueRunner runner;
    [SerializeField] string startNode = "Intro";
    [SerializeField] bool playOnStart = true;
    [Tooltip("Skip the intro after a death/restart in the same session (Main Menu > Start resets this).")]
    [SerializeField] bool onlyOncePerSession = true;
    [SerializeField] bool lockPlayerInput = true;

    [Header("Stage (Screen Space - Overlay canvas, below the Yarn dialogue canvas)")]
    [Tooltip("Root of the stage; faded out when the dialogue ends.")]
    [SerializeField] CanvasGroup stage;
    [SerializeField] Image background;
    [SerializeField] Image leftSlot, centerSlot, rightSlot;
    [SerializeField] List<VNCharacter> characters = new List<VNCharacter>();
    [SerializeField] List<Sprite> backgrounds = new List<Sprite>();
    [SerializeField] float fadeTime = 0.25f;

    [Header("Events")]
    public UnityEvent onStarted;
    public UnityEvent onFinished;

    public bool IsPlaying { get; private set; }

    static readonly HashSet<string> playedScenes = new HashSet<string>();
    readonly Dictionary<string, Image> slotOf = new Dictionary<string, Image>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Main Menu > Start calls this so the intro plays again on a new run.</summary>
    public static void ResetPlayed() => playedScenes.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => playedScenes.Clear();

    void Awake()
    {
        if (runner == null) runner = FindFirstObjectByType<DialogueRunner>();
        if (runner == null)
        {
            Debug.LogError("[VisualNovel] No DialogueRunner in the scene.", this);
            enabled = false;
            return;
        }

        runner.AddCommandHandler<string, string, string>("show", Show);
        runner.AddCommandHandler<string>("hide", Hide);
        runner.AddCommandHandler("hide_all", HideAll);
        runner.AddCommandHandler<string>("bg", SetBackground);
        runner.AddCommandHandler("bg_clear", ClearBackground);
        runner.AddCommandHandler<string>("bgm", name => SoundManager.Instance?.PlayBGM(name));
        runner.AddCommandHandler("stop_bgm", () => SoundManager.Instance?.StopBGM());
        runner.AddCommandHandler<string>("sfx", name => SoundManager.Instance?.PlaySFX(name));
        runner.AddCommandHandler<int>("load_scene", SceneLoader.Load);
        runner.onDialogueComplete?.AddListener(Finish);

        foreach (var slot in new[] { leftSlot, centerSlot, rightSlot })
            if (slot != null) slot.enabled = false;
        if (background != null && background.sprite == null) background.enabled = false;
        if (stage != null) stage.alpha = 0f;
    }

    void Start()
    {
        string scene = gameObject.scene.path;
        bool restartedAtCheckpoint = Checkpoint.TryGetSpawns(out _);
        if (playOnStart && !restartedAtCheckpoint && !(onlyOncePerSession && playedScenes.Contains(scene)))
            Play(startNode);
        else if (stage != null) stage.gameObject.SetActive(false);
    }

    /// <summary>Start a node (also usable from a trigger / UnityEvent mid-level).</summary>
    public void Play(string node)
    {
        if (IsPlaying || runner == null) return;
        IsPlaying = true;
        playedScenes.Add(gameObject.scene.path);
        if (stage != null)
        {
            stage.gameObject.SetActive(true);
            stage.DOFade(1f, fadeTime).SetLink(gameObject);
        }
        if (lockPlayerInput && PlayerController.Instance != null) PlayerController.Instance.InputEnabled = false;
        onStarted?.Invoke();
        _ = runner.StartDialogue(node);
    }

    void Finish()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        if (lockPlayerInput && PlayerController.Instance != null) PlayerController.Instance.InputEnabled = true;
        if (stage != null) stage.DOFade(0f, fadeTime).SetLink(gameObject).OnComplete(() => stage.gameObject.SetActive(false));
        onFinished?.Invoke();
    }

    #region Commands

    void Show(string characterId, string expression, string position)
    {
        VNCharacter character = characters.Find(c => c != null && string.Equals(c.id, characterId, System.StringComparison.OrdinalIgnoreCase));
        if (character == null)
        {
            Debug.LogWarning($"[VisualNovel] <<show>>: no VNCharacter with id '{characterId}'.", this);
            return;
        }

        Image slot = position.ToLowerInvariant() switch
        {
            "left" => leftSlot,
            "right" => rightSlot,
            _ => centerSlot
        };
        if (slot == null) return;

        // A character only stands in one place; anyone else in that slot is replaced.
        if (slotOf.TryGetValue(characterId, out var oldSlot) && oldSlot != slot) FadeOut(oldSlot);
        foreach (var pair in slotOf)
            if (pair.Value == slot && !string.Equals(pair.Key, characterId, System.StringComparison.OrdinalIgnoreCase)) { slotOf.Remove(pair.Key); break; }
        slotOf[characterId] = slot;

        bool wasHidden = !slot.enabled;
        slot.sprite = character.GetSprite(expression);
        slot.enabled = slot.sprite != null;
        slot.DOKill();
        Color c = character.portraitTint;
        if (wasHidden)
        {
            slot.color = new Color(c.r, c.g, c.b, 0f);
            slot.DOFade(c.a, fadeTime).SetLink(gameObject);
        }
        else
        {
            slot.color = c;
            slot.transform.DOKill();
            slot.transform.localScale = Vector3.one;
            slot.transform.DOPunchScale(Vector3.one * 0.04f, 0.2f, 4, 0.5f).SetLink(gameObject); // expression change pop
        }
    }

    void Hide(string characterId)
    {
        if (slotOf.TryGetValue(characterId, out var slot))
        {
            FadeOut(slot);
            slotOf.Remove(characterId);
        }
    }

    void HideAll()
    {
        foreach (var slot in slotOf.Values) FadeOut(slot);
        slotOf.Clear();
    }

    void SetBackground(string spriteName)
    {
        Sprite sprite = backgrounds.Find(s => s != null && string.Equals(s.name, spriteName, System.StringComparison.OrdinalIgnoreCase));
        if (sprite == null)
        {
            Debug.LogWarning($"[VisualNovel] <<bg>>: no background sprite named '{spriteName}'.", this);
            return;
        }
        background.sprite = sprite;
        background.enabled = true;
        background.color = new Color(1f, 1f, 1f, 0f);
        background.DOKill();
        background.DOFade(1f, fadeTime).SetLink(gameObject);
    }

    void ClearBackground()
    {
        if (background == null) return;
        background.DOKill();
        background.DOFade(0f, fadeTime).SetLink(gameObject).OnComplete(() => background.enabled = false);
    }

    void FadeOut(Image slot)
    {
        slot.DOKill();
        slot.DOFade(0f, fadeTime).SetLink(gameObject).OnComplete(() => slot.enabled = false);
    }

    #endregion
}
