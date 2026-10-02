using System.Collections.Generic;
using UnityEngine;

// Hold mouse / touch to draw. Release = stroke becomes solid.
public class ChalkDrawer : MonoBehaviour
{
    public ChalkPlayer player;
    public Camera cam;

    [Header("Drawing")]
    public float minPointDistance = 0.15f;
    public float costPerUnit = 2f;          // powder per world unit of line
    public float lineWidth = 0.12f;
    public bool strokesAreDynamic = false;  // true = strokes fall / roll like Crayon Physics

    [Header("Stroke lifetime")]
    public float baseLifetime = 8f;
    public float wetDurabilityBonus = 1.0f; // fully wet = lifetime * (1 + bonus)
    public float waterDissolveMultiplier = 4f;

    [Header("Layers")]
    public LayerMask drawableMask;          // chalk can only be drawn where the mouse is over these layers
    public LayerMask blockedMask;           // can't draw over these (Ground, Player, etc.) even if a drawable zone overlaps them
    public string strokeLayerName = "Chalk";
    public LayerMask waterMask;

    readonly List<Vector2> points = new List<Vector2>();
    LineRenderer preview;
    float lifetimeForThisStroke;

    void Start()
    {
        if (!cam) cam = Camera.main;
        preview = CreateLine(new GameObject("ChalkPreview"));
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            points.Clear();
            Vector2 start = MouseWorld();
            if (!CanDraw(start)) return;
            // wetness is locked in when you start the stroke
            lifetimeForThisStroke = baseLifetime * (1f + wetDurabilityBonus * player.Wetness);
            AddPoint(start);
        }
        else if (Input.GetMouseButton(0) && points.Count > 0)
        {
            Vector2 m = MouseWorld();
            float d = Vector2.Distance(points[points.Count - 1], m);
            if (d >= minPointDistance)
            {
                if (!SegmentDrawable(points[points.Count - 1], m)) Finish(); // left the drawable area
                else if (player.Spend(d * costPerUnit)) AddPoint(m);
                else Finish();                                  // out of chalk
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            Finish();
        }
    }

    void AddPoint(Vector2 p)
    {
        points.Add(p);
        preview.positionCount = points.Count;
        preview.SetPosition(points.Count - 1, p);
    }

    void Finish()
    {
        if (points.Count >= 2) BuildStroke();
        points.Clear();
        preview.positionCount = 0;
    }

    void BuildStroke()
    {
        var go = new GameObject("ChalkStroke");
        go.layer = LayerMask.NameToLayer(strokeLayerName);
        var stroke = go.AddComponent<ChalkStroke>();
        stroke.Init(points.ToArray(), lifetimeForThisStroke, waterMask,
                    waterDissolveMultiplier, lineWidth, strokesAreDynamic);
    }

    bool CanDraw(Vector2 p) =>
        Physics2D.OverlapPoint(p, drawableMask) && !Physics2D.OverlapPoint(p, blockedMask);

    // checks every step between two points so a fast mouse can't skip over another layer
    bool SegmentDrawable(Vector2 a, Vector2 b)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (minPointDistance * 0.5f)));
        for (int i = 1; i <= steps; i++)
            if (!CanDraw(Vector2.Lerp(a, b, (float)i / steps))) return false;
        return true;
    }

    Vector2 MouseWorld() => cam.ScreenToWorldPoint(Input.mousePosition);

    LineRenderer CreateLine(GameObject go)
    {
        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = lr.endWidth = lineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.startColor = lr.endColor = Color.white;
        lr.sortingOrder = 10;
        lr.positionCount = 0;
        return lr;
    }
}