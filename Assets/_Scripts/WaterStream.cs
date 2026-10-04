using UnityEngine;

// Waterfall running top to bottom. Anything solid in its path (ground or chalk strokes) cuts it off,
// so a roof drawn above the player leaves the area below dry.
// Place the object at the TOP-CENTER of the stream. Keep rotation 0 and scale 1.
// Each column has a trigger collider on the Water layer, so ChalkPlayer and ChalkStroke see it as water.
// Look: flowing streaks, bright sparkles, soft edges and splash spray where the water hits something.
public class WaterStream : MonoBehaviour
{
    [Header("Shape")]
    public float width = 1.5f;
    public float maxLength = 12f;
    public float columnWidth = 0.25f;     // thinner = more precise cut-off, slightly more cost
    public float penetration = 0.1f;      // water reaches this far into what it hits

    [Header("Blocking")]
    public LayerMask blockMask;           // Ground + Chalk (NOT Player, NOT Water)
    public string waterLayerName = "Water";

    [Header("Look")]
    public Color waterColor = new Color(0.55f, 0.82f, 1f, 0.8f);   // tint + overall opacity
    public float flowSpeed = 3f;          // how fast the streaks run down (world units/sec)
    public float streakLength = 2f;       // world length of one texture repeat
    [Range(0f, 1f)] public float topOpacity = 0.3f;                // water starts faint at the top
    public int sortingOrder = 8;

    [Header("Splash")]
    public bool splash = true;
    public float splashRate = 40f;        // particles/sec in total, shared by the columns that hit something

    class Column
    {
        public Transform tr;
        public BoxCollider2D col;
        public ParticleSystem ps;
    }

    Column[] columns;
    float colW;

    Mesh mesh;
    Vector3[] verts;
    Vector2[] uvs;
    Material mat, splashMat;
    Texture2D tex, dotTex;
    float scroll;

    void Awake()
    {
        int layer = LayerMask.NameToLayer(waterLayerName);
        if (layer < 0)
        {
            Debug.LogError("WaterStream: layer '" + waterLayerName + "' does not exist", this);
            layer = 0;
        }

        int n = Mathf.Max(1, Mathf.RoundToInt(width / columnWidth));
        colW = width / n;
        columns = new Column[n];

        tex = BuildWaterTexture();
        mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        mat.color = waterColor;

        dotTex = BuildDotTexture();
        splashMat = new Material(Shader.Find("Sprites/Default"));
        splashMat.mainTexture = dotTex;

        BuildMesh(n);

        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("StreamColumn" + i);
            go.layer = layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-width * 0.5f + colW * (i + 0.5f), 0f, 0f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            columns[i] = new Column { tr = go.transform, col = col, ps = splash ? BuildSplash(go.transform) : null };
        }
    }

    void OnDestroy()
    {
        if (mesh) Destroy(mesh);
        if (mat) Destroy(mat);
        if (splashMat) Destroy(splashMat);
        if (tex) Destroy(tex);
        if (dotTex) Destroy(dotTex);
    }

    void Update()
    {
        // scroll the streaks downward
        scroll = Mathf.Repeat(scroll - flowSpeed / Mathf.Max(0.1f, streakLength) * Time.deltaTime, 1f);
        mat.mainTextureOffset = new Vector2(0f, scroll);

        float perColumnSplash = splashRate / columns.Length;

        for (int i = 0; i < columns.Length; i++)
        {
            var c = columns[i];
            RaycastHit2D hit = Physics2D.Raycast(c.tr.position, Vector2.down, maxLength, blockMask);
            float len = hit ? hit.distance : maxLength;
            bool flowing = len > 0.05f;

            c.col.enabled = flowing;

            float bottom = flowing ? -len : 0f;
            verts[4 * i + 2].y = bottom;
            verts[4 * i + 3].y = bottom;
            float vBottom = flowing ? len / streakLength : 0f;
            uvs[4 * i + 2].y = vBottom;
            uvs[4 * i + 3].y = vBottom;

            if (flowing)
            {
                float colLen = hit ? len + penetration : len;
                c.col.size = new Vector2(colW, colLen);
                c.col.offset = new Vector2(0f, -colLen * 0.5f);
            }

            if (c.ps)
            {
                c.ps.transform.localPosition = new Vector3(0f, bottom, 0f);
                var em = c.ps.emission;
                em.rateOverTime = (flowing && hit) ? perColumnSplash : 0f;
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.RecalculateBounds();
    }

    // ---------- visuals ----------

    void BuildMesh(int n)
    {
        verts = new Vector3[4 * n];
        uvs = new Vector2[4 * n];
        var colors = new Color[4 * n];
        var tris = new int[6 * n];

        for (int i = 0; i < n; i++)
        {
            float x0 = -width * 0.5f + colW * i, x1 = x0 + colW;
            float u0 = (float)i / n, u1 = (float)(i + 1) / n;
            int v = 4 * i;

            verts[v]     = new Vector3(x0, 0f, 0f);  uvs[v]     = new Vector2(u0, 0f);
            verts[v + 1] = new Vector3(x1, 0f, 0f);  uvs[v + 1] = new Vector2(u1, 0f);
            verts[v + 2] = new Vector3(x0, 0f, 0f);  uvs[v + 2] = new Vector2(u0, 0f);
            verts[v + 3] = new Vector3(x1, 0f, 0f);  uvs[v + 3] = new Vector2(u1, 0f);

            colors[v] = colors[v + 1] = new Color(1f, 1f, 1f, topOpacity);   // soft start at the top
            colors[v + 2] = colors[v + 3] = Color.white;

            int t = 6 * i;
            tris[t] = v;     tris[t + 1] = v + 2; tris[t + 2] = v + 1;
            tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
        }

        mesh = new Mesh { name = "WaterStreamMesh" };
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = tris;

        var go = new GameObject("StreamVisual");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    ParticleSystem BuildSplash(Transform parent)
    {
        var go = new GameObject("Splash");
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // cone points up

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new Color(0.85f, 0.95f, 1f, 0.9f);
        main.gravityModifier = 2f;
        main.maxParticles = 60;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = colW * 0.5f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = splashMat;
        r.sortingOrder = sortingOrder + 1;

        ps.Play();
        return ps;
    }

    // streaks run along V (the flow), edges fade across U; wraps seamlessly along V so it can scroll
    Texture2D BuildWaterTexture()
    {
        int w = Mathf.Clamp(Mathf.RoundToInt(width * 48f), 32, 256), h = 128;
        var rng = new System.Random(7);
        var px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float a = v * Mathf.PI * 2f;
            float cx = Mathf.Cos(a), sy = Mathf.Sin(a);

            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;

                float n1 = Mathf.PerlinNoise(cx * 0.8f + 10f, sy * 0.8f + u * width * 9f);
                float n2 = Mathf.PerlinNoise(cx * 1.6f + 40f, sy * 1.6f + u * width * 22f);
                float streak = Mathf.SmoothStep(0.35f, 0.8f, n1 * 0.65f + n2 * 0.35f);

                float alpha = 0.5f + streak * 0.45f;
                float c = Mathf.Lerp(0.78f, 1f, streak);

                if (rng.NextDouble() < 0.015) { c = 1f; alpha = 1f; }   // sparkle

                alpha *= Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.08f);   // soft side edges
                px[y * w + x] = new Color(c, c, c, alpha);
            }
        }

        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapModeU = TextureWrapMode.Clamp;
        t.wrapModeV = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static Texture2D BuildDotTexture()
    {
        const int s = 32;
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d));
            }
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        Vector3 c = transform.position + Vector3.down * (maxLength * 0.5f);
        Gizmos.DrawWireCube(c, new Vector3(width, maxLength, 0f));
    }
}