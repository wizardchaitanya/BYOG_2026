using System.Collections.Generic;
using UnityEngine;

// Crusher / slammer. Rises (or slides) slowly along `direction`, pauses, then slams back fast.
// A chalk stroke in its path stops it for `holdTime` seconds, then the stroke breaks and it carries on.
// Works in any direction: up (floor crusher), left, right, down.
// Setup: object with a solid (non-trigger) Collider2D. Rigidbody2D is added and set to Kinematic automatically.
// Place the object at its REST position (where the slam ends).
[RequireComponent(typeof(Rigidbody2D))]
public class ChalkCrusher : MonoBehaviour
{
    [Header("Movement")]
    public Vector2 direction = Vector2.up;   // windup direction; the slam goes the opposite way
    public float distance = 3f;
    public float windupSpeed = 1.2f;         // slow
    public float slamSpeed = 20f;            // fast
    public float topPause = 0.6f;            // warning shake before the slam
    public float restPause = 1.5f;
    public float startDelay = 0f;            // offset crushers so they don't all sync

    [Header("Chalk interaction")]
    public float holdTime = 1f;              // how long a stroke holds it before breaking
    public LayerMask strokeMask;             // the Chalk layer

    [Header("Crushing")]
    public LayerMask playerMask;             // kills the player when the slam hits them

    enum Phase { Rest, Windup, Top, Slam, Held }

    Rigidbody2D rb;
    Vector2 dir, restPos;
    Phase phase, resumePhase;
    float travel, timer;
    ContactFilter2D windupFilter, slamFilter;
    readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    readonly List<ChalkStroke> blockers = new List<ChalkStroke>();
    const float Skin = 0.02f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        dir = direction.normalized;
        restPos = rb.position;

        windupFilter = new ContactFilter2D { useLayerMask = true, layerMask = strokeMask, useTriggers = false };
        slamFilter   = new ContactFilter2D { useLayerMask = true, layerMask = strokeMask | playerMask, useTriggers = false };

        phase = Phase.Rest;
        timer = startDelay;
    }

    void FixedUpdate()
    {
        if (!GameManager.IsPlaying) return;
        float dt = Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Rest:
                Place(0f, 0f);
                timer -= dt;
                if (timer <= 0f) phase = Phase.Windup;
                break;

            case Phase.Windup:
                Advance(windupSpeed * dt, false);
                break;

            case Phase.Top:
                Place(distance, 0.02f);          // shake = telegraph
                timer -= dt;
                if (timer <= 0f) phase = Phase.Slam;
                break;

            case Phase.Slam:
                Advance(-slamSpeed * dt, true);
                break;

            case Phase.Held:
                Place(travel, 0.04f);            // strain against the chalk
                timer -= dt;
                if (timer <= 0f) ReleaseHold();
                break;
        }
    }

    void Advance(float step, bool slam)
    {
        float dist = Mathf.Abs(step);
        Vector2 moveDir = step >= 0f ? dir : -dir;
        int n = rb.Cast(moveDir, slam ? slamFilter : windupFilter, hits, dist + Skin);

        float nearest = float.MaxValue;
        blockers.Clear();

        for (int i = 0; i < n; i++)
        {
            var h = hits[i];

            if (slam && InMask(h.collider, playerMask))
            {
                if (GameManager.Instance) GameManager.Instance.PlayerDied("Crushed");
                continue;
            }

            var stroke = h.collider.GetComponentInParent<ChalkStroke>();
            if (!stroke) continue;
            if (h.distance < nearest) nearest = h.distance;
            if (!blockers.Contains(stroke)) blockers.Add(stroke);
        }

        bool blocked = blockers.Count > 0;
        float allowed = blocked ? Mathf.Clamp(nearest - Skin, 0f, dist) : dist;

        travel = Mathf.Clamp(travel + (step >= 0f ? allowed : -allowed), 0f, distance);
        Place(travel, 0f);

        if (blocked)
        {
            resumePhase = phase;
            phase = Phase.Held;
            timer = holdTime;
        }
        else if (slam && travel <= 0.0001f)
        {
            travel = 0f; phase = Phase.Rest; timer = restPause;
            AudioManager.CrusherSlam(restPos);
        }
        else if (!slam && travel >= distance - 0.0001f)
        {
            travel = distance; phase = Phase.Top; timer = topPause;
        }
    }

    void ReleaseHold()
    {
        foreach (var s in blockers)
            if (s) s.Break();                    // may already have dissolved
        blockers.Clear();
        phase = resumePhase;
    }

    void Place(float t, float shake)
    {
        Vector2 p = restPos + dir * t;
        if (shake > 0f) p += Random.insideUnitCircle * shake;
        rb.MovePosition(p);
    }

    static bool InMask(Collider2D c, LayerMask mask) =>
        ((1 << c.gameObject.layer) & mask.value) != 0;

    void OnDrawGizmosSelected()
    {
        Vector2 d = direction.normalized;
        Vector3 start = Application.isPlaying ? (Vector3)restPos : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(start, start + (Vector3)(d * distance));
        Gizmos.DrawWireSphere(start + (Vector3)(d * distance), 0.2f);
    }
}