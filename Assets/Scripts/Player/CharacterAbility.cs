using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base class for a character's special ability ([F]) and optional interaction ([E]).
/// Put a subclass (TimeStop_Manager, Teleportation_Manager, ...) on any GameObject and drag it into
/// PlayerCharacter.ability. The character only forwards input while it is the selected one.
/// </summary>
public abstract class CharacterAbility : MonoBehaviour
{
    [Header("Ability Events")]
    public UnityEvent onAbilityUsed;
    [Tooltip("Pressed [F] but the ability could not be used (cooldown, no charges...). Good for a 'denied' sound.")]
    public UnityEvent onAbilityDenied;

    /// <summary>The character that owns this ability (set by PlayerCharacter).</summary>
    public PlayerCharacter Character { get; private set; }
    public bool IsSelected { get; private set; }

    public void Bind(PlayerCharacter character) => Character = character;

    /// <summary>Called by PlayerCharacter when [F] is pressed while selected.</summary>
    public void Trigger()
    {
        if (CanActivate() && Activate()) onAbilityUsed?.Invoke();
        else onAbilityDenied?.Invoke();
    }

    /// <summary>Called by PlayerCharacter when [E] is pressed while selected. Return true if handled.</summary>
    public virtual bool Interact() => false;

    public void SetSelected(bool selected)
    {
        if (IsSelected == selected) return;
        IsSelected = selected;
        OnSelectedChanged(selected);
    }

    public virtual bool CanActivate() => isActiveAndEnabled;

    /// <summary>Do the ability. Return false if it failed.</summary>
    protected abstract bool Activate();

    protected virtual void OnSelectedChanged(bool selected) { }
}
