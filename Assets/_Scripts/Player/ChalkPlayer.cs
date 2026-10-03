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
    public LayerMask coverMask;   // ground, platforms, AND chalk strokes (draw a roof = shelter)
    public LayerMask waterMask;   // water trigger zones
    public Transform feet;        // optional

    public float Wetness { get; private set; }
    public bool Sheltered { get; private set; }
    public bool InWater { get; private set; }

    void Update()
    {
        if (!GameManager.IsPlaying) return;   // paused, dead or level complete

        Vector2 p = transform.position;
        Sheltered = Physics2D.Raycast(p + Vector2.up * 0.6f, Vector2.up, 50f, coverMask);
        InWater = Physics2D.OverlapPoint(feet ? (Vector2)feet.position : p, waterMask);

        float rain = RainManager.Intensity;   // 0 = dry level, 1 = heavy rain
        float drain = 0f;
        if (InWater)                         { drain = waterDrain;        Wetness += wetGainRate * 3f * Time.deltaTime; }
        else if (!Sheltered && rain > 0.01f) { drain = rainDrain * rain;  Wetness += wetGainRate * rain * Time.deltaTime; }
        else                                 { Wetness -= wetLoseRate * Time.deltaTime; }

        Wetness = Mathf.Clamp01(Wetness);
        Damage(drain * Time.deltaTime);
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