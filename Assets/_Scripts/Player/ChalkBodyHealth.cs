using UnityEngine;

// Health shown on the body: chalk man gets shorter as chalk powder runs out.
// Feet stay planted; only the head lowers.
public class ChalkBodyHealth : MonoBehaviour
{
    public ChalkPlayer player;
    public Transform visual;            // child object holding the SpriteRenderer
    public Collider2D bodyCollider;     // BoxCollider2D or CapsuleCollider2D (vertical)
    public Transform wallCheck;         // optional, lowered with the body

    [Range(0.1f, 1f)] public float minHeight = 0.3f;  // height fraction at 0 health
    public float smooth = 12f;

    float fraction = 1f, fullHeight;

    // 0 = smallest, 1 = full size (smoothed). Used by the controller.
    public float Health01 => Mathf.InverseLerp(minHeight, 1f, fraction);
    Vector2 colSize, colOffset;
    Vector3 visScale, visPos, wallPos;

    void Awake()
    {
        if (!player) player = GetComponent<ChalkPlayer>();
        Capture();
    }

    public void Capture()
    {
        if (!player) player = GetComponent<ChalkPlayer>();

        if (bodyCollider is BoxCollider2D b) { colSize = b.size; colOffset = b.offset; }
        else if (bodyCollider is CapsuleCollider2D c) { colSize = c.size; colOffset = c.offset; }
        else Debug.LogError("ChalkBodyHealth needs a Box or Capsule collider");

        fullHeight = colSize.y;
        if (visual == transform)
        {
            Debug.LogError("ChalkBodyHealth: Visual must be a CHILD object, not the player root. Visual squash disabled.", this);
            visual = null;
        }
        if (visual)
        {
            visScale = visual.localScale;
            visPos = visual.localPosition;
        }
        if (wallCheck) wallPos = wallCheck.localPosition;
    }

    void LateUpdate()
    {
        float hp = Mathf.Clamp01(player.health / player.maxHealth);
        float target = Mathf.Lerp(minHeight, 1f, hp);
        fraction = Mathf.Lerp(fraction, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        float lost = fullHeight * (1f - fraction);

        // collider: shrink from the top, bottom stays put
        Vector2 size = new Vector2(colSize.x, colSize.y * fraction);
        Vector2 offset = new Vector2(colOffset.x, colOffset.y - lost * 0.5f);
        if (bodyCollider is BoxCollider2D b) { b.size = size; b.offset = offset; }
        else if (bodyCollider is CapsuleCollider2D c) { c.size = size; c.offset = offset; }

        // sprite: squash and keep feet in place (assumes sprite is centred on the collider)
        if (visual)
        {
            visual.localScale = new Vector3(visScale.x, visScale.y * fraction, visScale.z);
            visual.localPosition = new Vector3(visPos.x, visPos.y - lost * 0.5f, visPos.z);
        }

        if (wallCheck)
            wallCheck.localPosition = new Vector3(wallPos.x, wallPos.y - lost * 0.5f, wallPos.z);
    }
}