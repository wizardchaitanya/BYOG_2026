using System.Collections.Generic;
using UnityEngine;

// One drawn line: solid collider + dissolves over time (faster in water).
// Multiplayer: every client simulates its own copy; a "replica" is gently kept in step with the drawer's copy.
// Split-board MP: permanent = drawing-phase strokes never dissolve; strokes start frozen/hidden and are released when the race starts.
public class ChalkStroke : MonoBehaviour
{
    LineRenderer lr;
    readonly List<Collider2D> cols = new List<Collider2D>();
    Vector2[] localPoints;
    bool solid = true;
    float pendingExposure;
    Color baseColor = Color.white;

    public float Length { get; private set; }   // total drawn length
    public Rigidbody2D Body { get; private set; }
    public float Age01 => life > 0f ? Mathf.Clamp01(age / life) : 1f;

    float life, age, waterMult, width, rate = 1f, nextCheck;
    LayerMask waterMask;

    // ---- multiplayer replica sync ----
    const float CloseEnough = 0.25f;      // world units
    bool replica, hasNet, detached;   // detached = the local player touched it, so it follows local physics only
    bool frozen;
    Vector2 netPos, netVel;
    float netRot, netAngVel, netAge, netTime;
    public static bool Multiplayer;     // set by MPMatchManager
    public static readonly List<ChalkStroke> All = new List<ChalkStroke>();   // every live stroke (eraser lookup)
    public float ChalkCost;             // chalk this stroke cost to draw (refunded when erased in the drawing phase)
    public bool IsPermanent => permanent;
    bool permanent;                     // MP drawing-phase strokes never dissolve; race strokes do

    public void Init(Vector2[] pts, float lifetime, LayerMask water,
                     float waterDissolveMult, float lineWidth, bool dynamic, Color color)
    {
        All.Add(this);
        baseColor = color;
        life = lifetime; waterMask = water; waterMult = waterDissolveMult; width = lineWidth;

        // pivot at centroid so dynamic strokes rotate sensibly
        Vector2 c = Vector2.zero;
        foreach (var p in pts) c += p;
        c /= pts.Length;
        transform.position = c;

        var local = new Vector2[pts.Length];
        var local3 = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++) { local[i] = pts[i] - c; local3[i] = local[i]; }
        localPoints = local;

        lr = gameObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        ChalkVisuals.Apply(lr, width);
        lr.startWidth = lr.endWidth = width;
        lr.numCapVertices = 4; lr.numCornerVertices = 4;
        lr.sortingOrder = 5;
        lr.startColor = lr.endColor = baseColor;
        lr.positionCount = pts.Length;
        lr.SetPositions(local3);

        // One capsule per segment, each on its own child object (same Rigidbody2D).
        for (int i = 0; i < local.Length - 1; i++)
        {
            Vector2 a = local[i], b = local[i + 1];
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) continue;
            Length += len;

            var seg = new GameObject("Seg");
            seg.layer = gameObject.layer;
            seg.transform.SetParent(transform, false);
            seg.transform.localPosition = (a + b) * 0.5f;
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

            var cap = seg.AddComponent<CapsuleCollider2D>();
            cap.direction = CapsuleDirection2D.Horizontal;
            cap.size = new Vector2(len + width, width);
            cols.Add(cap);
        }

        if (dynamic)
        {
            Body = gameObject.AddComponent<Rigidbody2D>();
            Body.mass = Mathf.Max(0.2f, pts.Length * 0.02f);
        }
    }

    void Update()
    {
        if (Time.time >= nextCheck) { nextCheck = Time.time + 0.25f; CheckWater(); }

        if (permanent)
        {
            pendingExposure = 0f;
            age = 0f;
        }
        else
        {
            float streamRate = Mathf.Lerp(1f, waterMult, Mathf.Clamp01(pendingExposure));
            pendingExposure = 0f;
            age += Time.deltaTime * Mathf.Max(rate, streamRate);

            // replica: ease the fade timer toward the drawer's, so both copies dissolve together
            if (replica && hasNet)
                age = Mathf.Lerp(age, netAge + (Time.time - netTime), 0.15f);
        }

        float t = Mathf.Clamp01(age / life);

        var color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t * t);
        lr.startColor = lr.endColor = color;
        float w = width * Mathf.Lerp(1f, 0.3f, t);
        lr.startWidth = lr.endWidth = w;

        if (t > 0.85f && solid) SetSolid(false);

        if (t >= 1f)
        {
            // a replica normally waits for the drawer's "gone" message; self-destruct only if updates stopped
            bool waitForOwner = replica && hasNet && Time.time - netTime < 1f;
            if (!waitForOwner) Destroy(gameObject);
        }
    }

    void OnDestroy() { All.Remove(this); }

    // ---------- split-board helpers ----------

    // frozen = dynamic body does not simulate (no falling) until the race starts
    public void SetFrozen(bool on)
    {
        frozen = on;
        if (Body) Body.simulated = !on;
    }

    public void SetPermanent(bool on) { permanent = on; }

    public void SetVisible(bool on) { if (lr) lr.enabled = on; }

    // shortest world-space distance from a point to this stroke (used by the eraser)
    public float DistanceTo(Vector2 world)
    {
        if (localPoints == null || localPoints.Length == 0) return float.MaxValue;
        float best = float.MaxValue;
        Vector2 prev = transform.TransformPoint(localPoints[0]);
        if (localPoints.Length == 1) return Vector2.Distance(world, prev);

        for (int i = 1; i < localPoints.Length; i++)
        {
            Vector2 cur = transform.TransformPoint(localPoints[i]);
            Vector2 ab = cur - prev;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(world - prev, ab) / len2) : 0f;
            float d = Vector2.Distance(world, prev + ab * t);
            if (d < best) best = d;
            prev = cur;
        }
        return best;
    }

    // ---------- multiplayer ----------
    public void MarkReplica(float lag) { replica = true; age += lag; }

    public void SetNetState(Vector2 pos, float rot, float age01, Vector2 vel, float angVel, float lag)
    {
        netPos = pos; netRot = rot; netAge = age01 * life;
        netVel = vel; netAngVel = angVel;
        netTime = Time.time - lag;           // includes network delay
        hasNet = true;
    }

    void FixedUpdate()
    {
        if (frozen) return;

        if (Multiplayer && Body)
        {
            // safety net: a shove can never launch a stroke across the map
            Body.velocity = Vector2.ClampMagnitude(Body.velocity, 20f);
            Body.angularVelocity = Mathf.Clamp(Body.angularVelocity, -400f, 400f);
        }

        if (!replica || !hasNet || !Body) return;
        if (detached) return;
        if (netVel.sqrMagnitude > 0.09f || Mathf.Abs(netAngVel) > 20f) return; // drawer's copy still moving: both simulate on their own

        Vector2 err = netPos - Body.position;
        float angErr = Mathf.DeltaAngle(Body.rotation, netRot);
        if (err.magnitude < CloseEnough && Mathf.Abs(angErr) < 6f) return;

        float k = err.magnitude > 3f ? 1f : 0.2f;                             // small drift: ease. big drift: snap.
        Body.position = Vector2.Lerp(Body.position, netPos, k);
        Body.rotation += angErr * k;
        Body.velocity = Vector2.zero;
        Body.angularVelocity = 0f;
    }

    void OnCollisionEnter2D(Collision2D c) { NoteTouch(c); }
    void OnCollisionStay2D(Collision2D c) { NoteTouch(c); }

    void NoteTouch(Collision2D c)
    {
        if (!replica || detached) return;
        var p = c.collider.GetComponentInParent<ChalkPlayer>();
        if (p && !p.isRemote) detached = true;
    }

    // ---------- shared ----------
    public void HitByWater(float amount) { pendingExposure += amount; }

    void SetSolid(bool on)
    {
        solid = on;
        foreach (var c in cols) if (c) c.enabled = on;
    }

    public void Break()
    {
        SetSolid(false);
        AudioManager.StrokeBreak();
        Destroy(gameObject);
    }

    void CheckWater()
    {
        if (waterMask.value == 0) return;
        int hit = 0, step = Mathf.Max(1, localPoints.Length / 8), n = 0;
        for (int i = 0; i < localPoints.Length; i += step, n++)
            if (Physics2D.OverlapPoint(transform.TransformPoint(localPoints[i]), waterMask)) hit++;
        float frac = n > 0 ? (float)hit / n : 0f;
        rate = Mathf.Lerp(1f, waterMult, frac);
    }
}