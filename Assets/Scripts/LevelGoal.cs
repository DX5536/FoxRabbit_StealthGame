using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Reaching this trigger completes the level.</summary>
public class LevelGoal : MonoBehaviour
{
    [SerializeField] string message = "You made it out unseen.";
    [Tooltip("Both characters must be inside to finish.")]
    [SerializeField] bool requireBothCharacters = false;
    public UnityEvent onReached;

    readonly HashSet<PlayerCharacter> inside = new HashSet<PlayerCharacter>();

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var character = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<PlayerCharacter>() : null;
        if (character == null) return;
        inside.Add(character);

        int needed = requireBothCharacters && PlayerController.Instance != null ? PlayerController.Instance.Characters.Count : 1;
        if (inside.Count >= needed)
        {
            onReached?.Invoke();
            GameOver_Manager.TriggerLevelComplete(message);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var character = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<PlayerCharacter>() : null;
        if (character != null) inside.Remove(character);
    }
}
