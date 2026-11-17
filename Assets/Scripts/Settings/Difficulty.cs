using UnityEngine;

public enum DifficultyLevel { Easy, Normal, Hard }

/// <summary>
/// Game difficulty, chosen in Options and saved in PlayerPrefs.
/// Multipliers are applied on top of the values set in the Inspector / ScriptableObjects:
///   Easy   : Tetra recharges faster, time stop lasts longer, guards react slower.
///   Hard   : the opposite.
/// </summary>
public static class Difficulty
{
    const string Key = "difficulty";
    static DifficultyLevel? current;

    public static DifficultyLevel Current
    {
        get => current ??= (DifficultyLevel)PlayerPrefs.GetInt(Key, (int)DifficultyLevel.Normal);
        set
        {
            current = value;
            PlayerPrefs.SetInt(Key, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Tetra's hourglass refill time.</summary>
    public static float TimeStopCooldown => Pick(0.7f, 1f, 1.35f);
    /// <summary>How long a time stop lasts.</summary>
    public static float TimeStopDuration => Pick(1.3f, 1f, 0.75f);
    /// <summary>Seconds a guard needs to see you before you're caught.</summary>
    public static float GuardReactionTime => Pick(1.6f, 1f, 0.6f);

    static float Pick(float easy, float normal, float hard) =>
        Current == DifficultyLevel.Easy ? easy : Current == DifficultyLevel.Hard ? hard : normal;
}
