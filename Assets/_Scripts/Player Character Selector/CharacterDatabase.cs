using UnityEngine;

// The list of characters + the chalk colours the player can choose from.
// Create with: Project > Create > Chalk > Character Database
[CreateAssetMenu(menuName = "Chalk/Character Database", fileName = "CharacterDatabase")]
public class CharacterDatabase : ScriptableObject
{
    public ChalkCharacter[] characters;

    public Color[] palette =
    {
        Color.white,
        new Color(1.00f, 0.92f, 0.40f),   // yellow
        new Color(1.00f, 0.60f, 0.75f),   // pink
        new Color(0.55f, 0.82f, 1.00f),   // sky blue
        new Color(0.60f, 0.95f, 0.60f),   // green
        new Color(1.00f, 0.65f, 0.30f),   // orange
        new Color(0.85f, 0.65f, 1.00f),   // purple
    };

    public int Count => characters != null ? characters.Length : 0;

    public ChalkCharacter Get(int index)
    {
        if (Count == 0) return null;
        return characters[Mathf.Clamp(index, 0, Count - 1)];
    }
}
