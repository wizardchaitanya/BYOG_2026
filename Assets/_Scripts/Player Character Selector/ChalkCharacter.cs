using UnityEngine;

// One playable chalk character. Create with: right-click in Project > Create > Chalk > Character
[CreateAssetMenu(menuName = "Chalk/Character", fileName = "NewChalkCharacter")]
public class ChalkCharacter : ScriptableObject
{
    public string displayName = "Chalk";
    public Sprite sprite;                    // used in the game
    public Sprite previewSprite;             // optional bigger art for the menu (falls back to sprite)
    public Color defaultChalkColor = Color.white;   // line colour you get when picking this character

    [Header("Optional")]
    public RuntimeAnimatorController animator;      // if this character animates
    public float visualScale = 1f;                  // scale the sprite up/down without touching the collider

    [Header("Collider (only if this sprite is a different size from the default)")]
    public bool overrideCollider;
    public Vector2 colliderSize = new Vector2(0.8f, 1.8f);
    public Vector2 colliderOffset = Vector2.zero;

    public Sprite MenuSprite => previewSprite ? previewSprite : sprite;
}
