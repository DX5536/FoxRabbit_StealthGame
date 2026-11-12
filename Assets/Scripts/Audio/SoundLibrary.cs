using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Named sounds, so code, UnityEvents and Yarn scripts can say "door_open" instead of holding clips.
/// Create via Assets > Create > FoxRabbit > Sound Library. One library is enough for the whole game.
/// </summary>
[CreateAssetMenu(menuName = "FoxRabbit/Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Name used everywhere, e.g. click, jump, door_open, bgm_menu. Case-insensitive.")]
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch +/- this much, so repeated SFX don't sound robotic.")]
        [Range(0f, 0.5f)] public float pitchVariation = 0f;
    }

    public List<Entry> sounds = new List<Entry>();

    Dictionary<string, Entry> lookup;

    public Entry Find(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return null;
        if (lookup == null)
        {
            lookup = new Dictionary<string, Entry>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var e in sounds)
                if (e != null && !string.IsNullOrEmpty(e.name)) lookup[e.name] = e;
        }
        return lookup.TryGetValue(soundName, out var entry) ? entry : null;
    }

    void OnValidate() => lookup = null; // rebuild after edits in the Inspector
}
