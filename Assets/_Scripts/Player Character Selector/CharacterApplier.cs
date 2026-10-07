using UnityEngine;

// Put on the player. At level start it applies the character the player picked in the menu.
// Runs before ChalkBodyHealth so the shrink effect starts from the right size.
[DefaultExecutionOrder(-100)]
public class CharacterApplier : MonoBehaviour
{
    public CharacterDatabase database;
    public SpriteRenderer spriteRenderer;    // on the Visual child
    public Animator animator;                // optional
    public Collider2D bodyCollider;          // Box or Capsule, only needed for characters that override the collider
    public Transform groundCheck;            // moved to the new feet when the collider changes
    public Transform wallCheck;

    void Awake()
    {
        if (GetComponent<Photon.Pun.PhotonView>()) return;   // multiplayer prefab: NetworkPlayer applies it
        Apply(PlayerProfile.CharacterIndex);
    }

    public void Apply(int index)
    {
        if (!database || !spriteRenderer) return;
        var c = database.Get(index);
        if (!c) return;

        if (c.sprite) spriteRenderer.sprite = c.sprite;
        if (animator && c.animator) animator.runtimeAnimatorController = c.animator;
        if (!Mathf.Approximately(c.visualScale, 1f)) spriteRenderer.transform.localScale *= c.visualScale;
        if (c.overrideCollider && bodyCollider) ApplyCollider(c.colliderSize, c.colliderOffset);
    }

    void ApplyCollider(Vector2 size, Vector2 offset)
    {
        if (bodyCollider is BoxCollider2D b) { b.size = size; b.offset = offset; }
        else if (bodyCollider is CapsuleCollider2D cap) { cap.size = size; cap.offset = offset; }
        else return;

        if (groundCheck)
            groundCheck.localPosition = new Vector3(offset.x, offset.y - size.y * 0.5f, 0f);
        if (wallCheck)
        {
            float side = wallCheck.localPosition.x < 0f ? -1f : 1f;
            wallCheck.localPosition = new Vector3(offset.x + side * (size.x * 0.5f + 0.1f), offset.y, 0f);
        }
    }
}
