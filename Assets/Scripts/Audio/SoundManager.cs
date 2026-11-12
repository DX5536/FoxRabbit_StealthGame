using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Background music + sound effects for the whole game (adapted from the HannoverGameJam2026 AudioManager).
///  - Put one in EVERY scene (same SoundLibrary). The first one survives scene loads; later copies only
///    forward calls to it and start their scene's BGM, so UnityEvents in any scene can target "their" SoundManager.
///  - PlayBGM("name") crossfades. PlaySFX("name") plays a one-shot. Names come from the SoundLibrary.
///  - Every UI Button in every scene plays the click sound automatically.
///  - Music / SFX volume (0-1) are saved in PlayerPrefs.
/// Browsers only allow audio after the first click/key press; Unity resumes audio automatically then.
/// </summary>
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] SoundLibrary library;
    [Tooltip("BGM name this scene starts (empty = keep whatever is playing).")]
    [SerializeField] string sceneBGM = "";
    [Tooltip("Sound name played by every UI Button.")]
    [SerializeField] string clickSound = "click";
    [SerializeField] bool clickOnAllButtons = true;

    [Header("Volume (slider 0-1 maps to these maxima)")]
    [Range(0f, 1f)] [SerializeField] float musicMaxVolume = 0.4f;
    [Range(0f, 1f)] [SerializeField] float sfxMaxVolume = 1f;
    [SerializeField] float musicFadeTime = 0.5f;

    const string MusicKey = "vol_music01", SfxKey = "vol_sfx01";

    AudioSource music, sfx;
    string currentBgm;
    Tween musicFade;

    /// <summary>0-1 slider values.</summary>
    public float MusicVolume01 { get; private set; } = 1f;
    public float SfxVolume01 { get; private set; } = 1f;

    SoundManager Main => Instance != null ? Instance : this;

    void Awake()
    {
        if (Instance != null && Instance != this) return; // forwarder for this scene
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        MusicVolume01 = PlayerPrefs.GetFloat(MusicKey, 1f);
        SfxVolume01 = PlayerPrefs.GetFloat(SfxKey, 1f);
        music.volume = MusicTarget;
        SceneManager.sceneLoaded += OnSceneLoaded; // also fires for the first scene (after Awake)
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(sceneBGM)) Main.PlayBGM(sceneBGM);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    #region Public API (UnityEvent / Yarn friendly)

    public void PlaySFX(string soundName) => Main.PlayEntry(soundName);
    public void PlaySFX(AudioClip clip) { if (clip != null) Main.sfx.PlayOneShot(clip, Main.sfxMaxVolume * Main.SfxVolume01); }
    public void PlayClick() => PlaySFX(clickSound);

    /// <summary>Crossfade to a library BGM. Same track again = keeps playing.</summary>
    public void PlayBGM(string soundName)
    {
        var m = Main;
        if (string.Equals(soundName, m.currentBgm, System.StringComparison.OrdinalIgnoreCase) && m.music.isPlaying) return;
        var entry = m.library != null ? m.library.Find(soundName) : null;
        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"[SoundManager] No BGM named '{soundName}' in the SoundLibrary.", this);
            return;
        }
        m.currentBgm = soundName;
        m.musicFade?.Kill();
        m.musicFade = DOTween.Sequence()
            .Append(m.music.DOFade(0f, m.music.isPlaying ? m.musicFadeTime * 0.5f : 0f))
            .AppendCallback(() => { m.music.clip = entry.clip; m.music.Play(); })
            .Append(m.music.DOFade(m.MusicTarget, m.musicFadeTime))
            .SetUpdate(true).SetLink(m.gameObject);
    }

    public void StopBGM()
    {
        var m = Main;
        m.currentBgm = null;
        m.musicFade?.Kill();
        m.musicFade = m.music.DOFade(0f, m.musicFadeTime).SetUpdate(true).SetLink(m.gameObject).OnComplete(m.music.Stop);
    }

    public void SetMusicVolume(float value01)
    {
        var m = Main;
        m.MusicVolume01 = Mathf.Clamp01(value01);
        PlayerPrefs.SetFloat(MusicKey, m.MusicVolume01);
        m.musicFade?.Kill();
        m.music.volume = m.MusicTarget;
    }

    public void SetSfxVolume(float value01)
    {
        var m = Main;
        m.SfxVolume01 = Mathf.Clamp01(value01);
        PlayerPrefs.SetFloat(SfxKey, m.SfxVolume01);
    }

    #endregion

    float MusicTarget => musicMaxVolume * MusicVolume01;

    void PlayEntry(string soundName)
    {
        var entry = library != null ? library.Find(soundName) : null;
        if (entry == null || entry.clip == null) return; // missing sounds are fine while prototyping
        sfx.pitch = 1f + Random.Range(-entry.pitchVariation, entry.pitchVariation);
        sfx.PlayOneShot(entry.clip, entry.volume * sfxMaxVolume * SfxVolume01);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!clickOnAllButtons) return;
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.gameObject.scene == scene) button.onClick.AddListener(PlayClick);
    }
}
