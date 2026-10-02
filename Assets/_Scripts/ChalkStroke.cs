using UnityEngine;

// One drawn line: solid collider + dissolves over time (faster in water).
public class ChalkStroke : MonoBehaviour
{
    LineRenderer lr;
    PolygonCollider2D col;
    Vector2[] worldPoints;
    float life, age, waterMult, width, rate = 1f, nextCheck;
    LayerMask waterMask;

    public void Init(Vector2[] pts, float lifetime, LayerMask water,
                     float waterDissolveMult, float lineWidth, bool dynamic)
    {
        worldPoints = pts;
        life = lifetime; waterMask = water; waterMult = waterDissolveMult; width = lineWidth;

        // pivot at centroid so dynamic strokes rotate sensibly
        Vector2 c = Vector2.zero;
        foreach (var p in pts) c += p;
        c /= pts.Length;
        transform.position = c;

        var local = new Vector2[pts.Length];
        var local3 = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++) { local[i] = pts[i] - c; local3[i] = local[i]; }

        lr = gameObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = lr.endWidth = width;
        lr.numCapVertices = 4; lr.numCornerVertices = 4;
        lr.sortingOrder = 5;
        lr.positionCount = pts.Length;
        lr.SetPositions(local3);

        // one quad per segment: edge colliders can't collide with each other, polygons can
        col = gameObject.AddComponent<PolygonCollider2D>();
        col.pathCount = local.Length - 1;
        float h = width * 0.5f;
        for (int i = 0; i < local.Length - 1; i++)
        {
            Vector2 a = local[i], b = local[i + 1];
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * h;
            Vector2 ext = dir * h;
            col.SetPath(i, new Vector2[] { a - ext + n, b + ext + n, b + ext - n, a - ext - n });
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

        age += Time.deltaTime * rate;
        float t = Mathf.Clamp01(age / life);

        var color = new Color(1f, 1f, 1f, 1f - t * t);
        lr.startColor = lr.endColor = color;
        float w = width * Mathf.Lerp(1f, 0.3f, t);
        lr.startWidth = lr.endWidth = w;

        if (t > 0.85f && col.enabled) col.enabled = false; // stops being solid before vanishing
        if (t >= 1f) Destroy(gameObject);
    }

    // fraction of the stroke sitting in water -> faster dissolve
    void CheckWater()
    {
        if (waterMask.value == 0) return;
        int hit = 0, step = Mathf.Max(1, worldPoints.Length / 8), n = 0;
        for (int i = 0; i < worldPoints.Length; i += step, n++)
            if (Physics2D.OverlapPoint(transform.TransformPoint(worldPoints[i] - (Vector2)transform.position), waterMask)) hit++;
        float frac = n > 0 ? (float)hit / n : 0f;
        rate = Mathf.Lerp(1f, waterMult, frac);
    }
}