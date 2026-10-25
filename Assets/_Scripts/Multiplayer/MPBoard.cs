using UnityEngine;

// Drawing board for the split-board MP mode. One per MP map.
// - Player 1 (index 0) draws on the LEFT half, player 2 on the RIGHT half.
// - noDrawZones = areas nobody can draw in (start, end, top, etc.)
// - During the drawing phase a fog covers the OPPONENT's half (hides their strokes, start/end markers, everything).
// All colliders here are only used for point tests: set them to Trigger, any layer.
public class MPBoard : MonoBehaviour
{
    public static MPBoard Instance { get; private set; }

    [Header("Area")]
    public Collider2D board;                 // whole drawable board (trigger BoxCollider2D). Its centre X = the split line.
    public Collider2D[] noDrawZones;         // trigger BoxCollider2Ds
    public float midGap = 0.2f;              // keeps strokes this far from the split line

    [Header("Look")]
    public Color fogColor = new Color(0.12f, 0.14f, 0.16f, 0.97f);
    public Color blockedColor = new Color(1f, 0.25f, 0.25f, 0.18f);
    public Color midLineColor = new Color(1f, 1f, 1f, 0.35f);
    public float fogFade = 0.5f;
    public float fogOverflow = 40f;          // fog extends this far past the board so no edge shows
    public int fogSortingOrder = 50;         // above everything except UI (name tags are 20, strokes 5-10)

    int myIndex;
    float midX;
    SpriteRenderer fog;
    GameObject overlays;
    float fogTarget, fogAlpha;
    static Sprite square, cloud;

    void Awake()
    {
        Instance = this;
        if (!board) { Debug.LogError("MPBoard: assign the Board collider", this); enabled = false; return; }
        midX = board.bounds.center.x;
        BuildOverlays();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // called by MPMatchManager once we know which player we are
    public void Configure(int index)
    {
        myIndex = index;
        if (fog) Destroy(fog.gameObject);

        Bounds b = board.bounds;
        float xMin, xMax;
        if (index == 0) { xMin = midX; xMax = b.max.x + fogOverflow; }          // I'm left: fog the right half
        else            { xMin = b.min.x - fogOverflow; xMax = midX; }          // I'm right: fog the left half
        float yMin = b.min.y - fogOverflow, yMax = b.max.y + fogOverflow;

        fog = MakeSprite("Fog", CloudSprite(), fogColor, fogSortingOrder,
                         new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f),
                         new Vector2(xMax - xMin, yMax - yMin));
        fog.transform.SetParent(transform, true);
        fog.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0f);
        fogAlpha = 0f;
    }

    // true = drawing phase look (fog, red zones, split line). false = full board revealed.
    public void SetDrawingView(bool on)
    {
        fogTarget = on ? 1f : 0f;
        if (overlays) overlays.SetActive(on);
    }

    public bool CanDraw(Vector2 p)
    {
        if (!board || !board.OverlapPoint(p)) return false;
        if (myIndex == 0) { if (p.x > midX - midGap) return false; }
        else if (p.x < midX + midGap) return false;
        foreach (var z in noDrawZones)
            if (z && z.OverlapPoint(p)) return false;
        return true;
    }

    void Update()
    {
        if (!fog) return;
        fogAlpha = Mathf.MoveTowards(fogAlpha, fogTarget, Time.unscaledDeltaTime / Mathf.Max(0.01f, fogFade));
        fog.color = new Color(fogColor.r, fogColor.g, fogColor.b, fogColor.a * fogAlpha);
        fog.enabled = fogAlpha > 0.001f;
    }

    // ---------- visuals ----------
    void BuildOverlays()
    {
        overlays = new GameObject("BoardOverlays");
        overlays.transform.SetParent(transform, false);

        foreach (var z in noDrawZones)
        {
            if (!z) continue;
            var sr = MakeSprite("NoDraw", SquareSprite(), blockedColor, 4, z.bounds.center, z.bounds.size);
            sr.transform.SetParent(overlays.transform, true);
        }

        Bounds b = board.bounds;
        var line = MakeSprite("SplitLine", SquareSprite(), midLineColor, fogSortingOrder + 1,
                              new Vector2(midX, b.center.y), new Vector2(0.06f, b.size.y));
        line.transform.SetParent(overlays.transform, true);
        overlays.SetActive(false);
    }

    static SpriteRenderer MakeSprite(string name, Sprite s, Color c, int order, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = c;
        sr.sortingOrder = order;
        var sz = s.bounds.size;
        go.transform.localScale = new Vector3(size.x / sz.x, size.y / sz.y, 1f);
        return sr;
    }

    static Sprite SquareSprite()
    {
        if (square) return square;
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, Color.white);
        t.Apply();
        square = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return square;
    }

    // low-res noise stretched with bilinear filtering = soft frosted look
    static Sprite CloudSprite()
    {
        if (cloud) return cloud;
        const int n = 32;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = Mathf.PerlinNoise(x * 0.15f + 50f, y * 0.15f + 50f);
                float c = Mathf.Lerp(0.75f, 1f, v);
                px[y * n + x] = new Color(c, c, c, 1f);
            }
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        t.SetPixels(px);
        t.Apply();
        cloud = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 world unit
        return cloud;
    }

    void OnDrawGizmosSelected()
    {
        if (board)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(board.bounds.center, board.bounds.size);
            Gizmos.DrawLine(new Vector3(board.bounds.center.x, board.bounds.min.y), new Vector3(board.bounds.center.x, board.bounds.max.y));
        }
        Gizmos.color = Color.red;
        if (noDrawZones != null)
            foreach (var z in noDrawZones) if (z) Gizmos.DrawWireCube(z.bounds.center, z.bounds.size);
    }
}