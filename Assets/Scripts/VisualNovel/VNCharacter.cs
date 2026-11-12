using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A visual novel character. Create via Assets > Create > FoxRabbit > VN Character.
/// Id must match the speaker name in Yarn ("Tetra: Hello") and the <<show Tetra happy left>> command.
/// </summary>
[CreateAssetMenu(menuName = "FoxRabbit/VN Character", fileName = "VNCharacter")]
public class VNCharacter : ScriptableObject
{
    [Tooltip("Used in Yarn: <<show id expression position>>. Use the same text as the speaker name (\"Tetra: Hi\"). Case-insensitive.")]
    public string id = "Tetra";
    [Tooltip("Tint for the portrait (handy for placeholder art).")]
    public Color portraitTint = Color.white;

    [System.Serializable]
    public class Expression
    {
        [Tooltip("e.g. neutral, happy, mad, surprised")]
        public string name = "neutral";
        public Sprite sprite;
    }

    [Tooltip("First entry is the default expression.")]
    public List<Expression> expressions = new List<Expression>();

    /// <summary>Sprite for an expression; falls back to the first one.</summary>
    public Sprite GetSprite(string expression)
    {
        foreach (var e in expressions)
            if (string.Equals(e.name, expression, System.StringComparison.OrdinalIgnoreCase)) return e.sprite;
        return expressions.Count > 0 ? expressions[0].sprite : null;
    }
}
