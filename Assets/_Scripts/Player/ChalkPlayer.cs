using UnityEngine;

// Chalk man: health = chalk powder. Drains in rain / water, drawing costs powder.
public class ChalkPlayer : MonoBehaviour
{
    [Header("Health (chalk powder)")]
    public float maxHealth = 100f;
    public float health = 100f;

    [Header("Drain per second")]
    public float rainDrain = 2f;
    public float waterDrain = 8f;

    [Header("Wetness (0..1)")]
    public float wetGainRate = 0.25f;
    public float wetLoseRate = 0.1f;

    [Header("Detection")]
    public LayerMask coverMask;   // ONLY these layers shelter from rain, e.g. Shelter + Chalk (draw a roof = shelter)
    public float maxShelterHeight = 6f;   // cover higher than this above the head is ignored
    public LayerMask waterMask;   // water trigger zones
    public Transform feet;        // optional
    public float waterCheckRadius = 0.15f;

    [Header("Debug (read-only, watch these while playing)")]
    public float debugRainIntensity;
    public bool debugSheltered;
    public string debugShelteredBy;

    Collider2D bodyCol;
    readonly RaycastHit2D[] rayHits = new RaycastHit2D[8];

    public float Wetness { get; private set; }
    public bool Sheltered { get; private set; }
    public bool InWater { get; private set; }

    void Awake()
    {
        foreach (var c in GetComponentsInChildren<Collider2D>())
            if (!c.isTrigger) { bodyCol = c; break; }
    }

    void Update()
    {
        if (!GameManager.IsPlaying) return;   // paused, dead or level complete

        Vector2 p = transform.position;
        Sheltered = CheckShelter();
        Vector2 f = feet ? (Vector2)feet.position : p;
        InWater = Physics2D.OverlapCircle(f, waterCheckRadius, waterMask)
               || Physics2D.OverlapCircle(p, waterCheckRadius, waterMask);

        float rain = RainManager.Intensity;   // 0 = dry level, 1 = heavy rain
        debugRainIntensity = rain;
        debugSheltered = Sheltered;
        float drain = 0f;
        if (InWater)                         { drain = waterDrain;        Wetness += wetGainRate * 3f * Time.deltaTime; }
        else if (!Sheltered && rain > 0.01f) { drain = rainDrain * rain;  Wetness += wetGainRate * rain * Time.deltaTime; }
        else                                 { Wetness -= wetLoseRate * Time.deltaTime; }

        Wetness = Mathf.Clamp01(Wetness);
        Damage(drain * Time.deltaTime);
    }

    // Is there cover above the head? Ignores triggers and the player's own colliders.
    bool CheckShelter()
    {
        debugShelteredBy = "";
        Vector2 origin = bodyCol
            ? new Vector2(bodyCol.bounds.center.x, bodyCol.bounds.max.y + 0.05f)
            : (Vector2)transform.position + Vector2.up * 0.6f;

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = coverMask, useTriggers = false };
        int n = Physics2D.Raycast(origin, Vector2.up, filter, rayHits, maxShelterHeight);

        for (int i = 0; i < n; i++)
        {
            var c = rayHits[i].collider;
            if (c.transform.IsChildOf(transform)) continue;   // own body
            debugShelteredBy = c.name;
            return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(feet ? feet.position : transform.position, waterCheckRadius);
        Gizmos.DrawWireSphere(transform.position, waterCheckRadius);
    }

    public bool Spend(float amount)
    {
        if (health <= 0f) return false;
        Damage(amount);
        return true;
    }

    void Damage(float amount)
    {
        health = Mathf.Max(0f, health - amount);
        if (health <= 0f) Die();
    }

    void Die()
    {
        // TODO: crumble animation
        if (GameManager.Instance)
            GameManager.Instance.PlayerDied(InWater ? "Dissolved in the water" : "Ran out of chalk");
        enabled = false;
    }
}