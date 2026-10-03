using UnityEngine;

// Waterfall running top to bottom. Anything solid in its path (ground or chalk strokes) cuts it off,
// so a roof drawn above the player leaves the area below dry.
// Place the object at the TOP-CENTER of the stream. Keep rotation 0 and scale 1.
// Each column is a trigger on the Water layer, so ChalkPlayer and ChalkStroke see it as water automatically.
public class WaterStream : MonoBehaviour
{
    [Header("Shape")]
    public float width = 1.5f;
    public float maxLength = 12f;
    public float columnWidth = 0.25f;     // thinner = more precise cut-off, slightly more cost
    public float penetration = 0.1f;      // water reaches this far into what it hits, so it wets the roof's top edge

    [Header("Blocking")]
    public LayerMask blockMask;           // Ground + Chalk (NOT Player, NOT Water)
    public string waterLayerName = "Water";

    [Header("Look")]
    public Color waterColor = new Color(0.35f, 0.6f, 1f, 0.6f);
    public int sortingOrder = 8;

    class Column
    {
        public Transform tr;
        public BoxCollider2D col;
        public LineRenderer line;
    }

    Column[] columns;
    float colW;

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
        var mat = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("StreamColumn" + i);
            go.layer = layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-width * 0.5f + colW * (i + 0.5f), 0f, 0f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.material = mat;
            line.positionCount = 2;
            line.startWidth = line.endWidth = colW * 1.05f;   // slight overlap so columns have no seams
            line.startColor = line.endColor = waterColor;
            line.sortingOrder = sortingOrder;
            line.SetPosition(0, Vector3.zero);

            columns[i] = new Column { tr = go.transform, col = col, line = line };
        }
    }

    void Update()
    {
        foreach (var c in columns)
        {
            RaycastHit2D hit = Physics2D.Raycast(c.tr.position, Vector2.down, maxLength, blockMask);
            float len = hit ? hit.distance : maxLength;
            bool flowing = len > 0.05f;

            c.line.enabled = flowing;
            c.col.enabled = flowing;
            if (!flowing) continue;

            float colLen = hit ? len + penetration : len;
            c.line.SetPosition(1, new Vector3(0f, -len, 0f));
            c.col.size = new Vector2(colW, colLen);
            c.col.offset = new Vector2(0f, -colLen * 0.5f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        Vector3 c = transform.position + Vector3.down * (maxLength * 0.5f);
        Gizmos.DrawWireCube(c, new Vector3(width, maxLength, 0f));
    }
}
