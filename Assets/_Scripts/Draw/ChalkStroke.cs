using System.Collections.Generic;
using UnityEngine;

// One drawn line: solid collider + dissolves over time (faster in water).
public class ChalkStroke : MonoBehaviour
{
    LineRenderer lr;
    readonly List<Collider2D> cols = new List<Collider2D>();
    Vector2[] localPoints;
    bool solid = true;
    float pendingExposure;

    public float Length { get; private set; }   // total drawn length
    float life, age, waterMult, width, rate = 1f, nextCheck;
    LayerMask waterMask;

    public void Init(Vector2[] pts, float lifetime, LayerMask water,
                     float waterDissolveMult, float lineWidth, bool dynamic)
    {
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
        lr.positionCount = pts.Length;
        lr.SetPositions(local3);

        // One capsule per segment, each on its own child object (same Rigidbody2D).
        // Separate colliders may overlap freely. (Overlapping paths inside ONE PolygonCollider2D
        // cut holes in each other, which let water and objects slip through straight lines.)
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
            cap.size = new Vector2(len + width, width);   // rounded ends cover the joints
            cols.Add(cap);
        }

        if (dynamic)
        {
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.mass = Mathf.Max(0.2f, pts.Length * 0.02f);
        }
    }

    void Update()
    {
        if (Time.time >= nextCheck) { nextCheck = Time.time + 0.25f; CheckWater(); }

        // water falling straight onto this stroke (WaterStream reports it) dissolves it faster
        float streamRate = Mathf.Lerp(1f, waterMult, Mathf.Clamp01(pendingExposure));
        pendingExposure = 0f;
        age += Time.deltaTime * Mathf.Max(rate, streamRate);
        float t = Mathf.Clamp01(age / life);

        var color = new Color(1f, 1f, 1f, 1f - t * t);
        lr.startColor = lr.endColor = color;
        float w = width * Mathf.Lerp(1f, 0.3f, t);
        lr.startWidth = lr.endWidth = w;

        if (t > 0.85f && solid) SetSolid(false);   // stops being solid before vanishing
        if (t >= 1f) Destroy(gameObject);
    }

    // called every frame by a WaterStream column that is landing on this stroke
    public void HitByWater(float amount) { pendingExposure += amount; }

    void SetSolid(bool on)
    {
        solid = on;
        foreach (var c in cols) if (c) c.enabled = on;
    }

    // called by crushers etc. to snap the stroke
    public void Break()
    {
        SetSolid(false);
        AudioManager.StrokeBreak();
        Destroy(gameObject);
    }

    // fraction of the stroke sitting in water -> faster dissolve
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