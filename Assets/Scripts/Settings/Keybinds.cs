using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameAction { MoveLeft, MoveRight, Jump, Sprint, SwitchCharacter, Ability, Interact, ShowCircuit }

/// <summary>
/// Rebindable keyboard keys, saved in PlayerPrefs (works in WebGL: stored in the browser).
/// PlayerController / CircuitView read keys from here; the Options panel rebinds them.
/// </summary>
public static class Keybinds
{
    static readonly Dictionary<GameAction, Key> Defaults = new Dictionary<GameAction, Key>
    {
        { GameAction.MoveLeft, Key.A },
        { GameAction.MoveRight, Key.D },
        { GameAction.Jump, Key.Space },
        { GameAction.Sprint, Key.LeftShift },
        { GameAction.SwitchCharacter, Key.Q },
        { GameAction.Ability, Key.F },
        { GameAction.Interact, Key.E },
        { GameAction.ShowCircuit, Key.LeftShift }, // shares Shift with sprint on purpose (holding it while moving is fine)
    };

    static Dictionary<GameAction, Key> current;

    /// <summary>Raised after any key changes (UI labels refresh on this).</summary>
    public static event Action Changed;

    public static Key Get(GameAction action)
    {
        Load();
        return current[action];
    }

    /// <summary>Short display name for prompts, e.g. "E" or "Space".</summary>
    public static string Label(GameAction action)
    {
        Key key = Get(action);
        var kb = Keyboard.current;
        return kb != null ? kb[key].displayName : key.ToString();
    }

    /// <summary>Bind a key. An action already using it gets this action's old key (swap), so nothing is left unbound.</summary>
    public static void Set(GameAction action, Key key)
    {
        Load();
        Key old = current[action];
        foreach (GameAction other in Enum.GetValues(typeof(GameAction)))
            if (other != action && current[other] == key && !SharedByDefault(action, other)) current[other] = old;
        current[action] = key;
        Save();
    }

    public static void ResetAll()
    {
        current = new Dictionary<GameAction, Key>(Defaults);
        Save();
    }

    static bool SharedByDefault(GameAction a, GameAction b) => Defaults[a] == Defaults[b];

    static void Load()
    {
        if (current != null) return;
        current = new Dictionary<GameAction, Key>();
        foreach (var pair in Defaults)
            current[pair.Key] = (Key)PlayerPrefs.GetInt("key_" + pair.Key, (int)pair.Value);
    }

    static void Save()
    {
        foreach (var pair in current) PlayerPrefs.SetInt("key_" + pair.Key, (int)pair.Value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
