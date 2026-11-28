using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Hold mouse / touch to draw. Release = stroke becomes solid.
// Touch: one finger is the "draw finger"; other fingers (move buttons) are ignored.
// Split-board MP: draw mode (static/dynamic) and eraser are set by MPDrawUI; drawing is free (no chalk cost)
// and limited to the player's half by MPBoard.
public class ChalkDrawer : MonoBehaviour
{
    public ChalkPlayer player;
    public Camera cam;

    [Header("Drawing")]
    public float minPointDistance = 0.15f;
    public float costPerUnit = 2f;          // powder per world unit of line
    public float lineWidth = 0.15f;
    public Texture2D chalkTexture;          // optional: your own chalk PNG (leave empty for the built-in one)
    public bool strokesAreDynamic = false;  // only used when Auto Static is off
    [Header("Auto static / dynamic")]
    public bool autoStatic = true;          // both ends touching something solid = static, otherwise it falls
    public LayerMask anchorMask;            // what counts as solid: Ground, platforms, objects, Chalk
    public float anchorRadius = 0.3f;       // how close an end must be to count as touching

    [Header("Stroke lifetime")]
    public float baseLifetime = 8f;
    public float wetDurabilityBonus = 1.0f; // fully wet = lifetime * (1 + bonus)
    public float waterDissolveMultiplier = 4f;

    [Header("Layers")]
    public LayerMask drawableMask;          // chalk can only be drawn where the pointer is over these layers
    public LayerMask blockedMask;           // can't draw over these (Ground, Player, etc.) even if a drawable zone overlaps them
    public string strokeLayerName = "Chalk";
    public LayerMask waterMask;

    [Header("Touch")]
    public float touchOffsetMm = 6f;        // draw this far ABOVE the finger so the tip stays visible (0 = off)

    [Header("Split-board MP")]
    public bool dynamicMode;                // MP: true = strokes fall / move, false = static
    public bool erasing;                    // MP: pointer erases your own strokes
    public float eraseRadius = 0.4f;

    [Header("Cursor (desktop)")]
    public Texture2D chalkCursor;
    public Texture2D eraserCursor;
    public Vector2 chalkHotspot;     // pixel in the texture that is the tip
    public Vector2 eraserHotspot;
    int cursorState = -1;            // 0 default, 1 chalk, 2 eraser
    Vector2 appliedHotspot;

    [Header("Player no-draw border")]
    public float playerBorderRadius = 0.6f;   // world units around the player's body, 0 = off

    [Header("Race stroke cooldown")]
    public float strokeCooldown = 0.4f;       // seconds before the next stroke can start (race only)

    [Header("Tutorial")]
    public static bool DrawLocked;     // tutorial: drawing disabled
    public bool tutorialMode;          // tutorial: eraser works without MPMatchManager
    float nextStrokeTime;
    Collider2D bodyCol;

    readonly List<Vector2> points = new List<Vector2>();
    readonly List<ChalkStroke> mine = new List<ChalkStroke>();   // strokes this player drew (the eraser only touches these)

    public bool IsDrawing => points.Count > 0;   // true while a stroke is being drawn
    public Vector2 Tip { get; private set; }     // where the chalk is right now (world space)
    LineRenderer preview;
    float lifetimeForThisStroke;
    Color strokeColor = Color.white;

    bool holding;       // pointer is down (may be outside the drawable area)
    int drawFinger = -1;
    bool pointerIsTouch;

    public event System.Action<Color> StrokeBegan;
    public event System.Action<Vector2> PointAdded;
    public event System.Action StrokeEnded;

    public ChalkStroke LastStroke { get; private set; }

    // field
    public event System.Action<Vector2[], float, bool, Color> StrokeCreated;

    // MPDrawUI hooks
    // split-board MP: static/dynamic buttons and the eraser only exist in the drawing phase
    static bool MPDrawingPhase => MPMatchManager.Instance != null && MPMatchManager.Instance.Phase == MPPhase.Drawing;

    public void SetDynamic(bool on) { dynamicMode = on; erasing = false; }
    public void SetErasing(bool on) { erasing = on; }

    public event System.Action EraseDone;
    bool erasedThisHold;

    void Start()
    {
        if (!cam) cam = Camera.main;
        if (Application.isMobilePlatform) Input.simulateMouseWithTouches = false;   // we read touches ourselves
        ChalkVisuals.SetCustomTexture(chalkTexture);
        preview = CreateLine(new GameObject("ChalkPreview"));
    }

    void Update()
    {
        bool allowed = GameManager.Instance != null ? GameManager.IsPlaying : MPMatchManager.DrawAllowed;
        if (MPMatchManager.LocalPaused) allowed = false;
        if (TutorialManager.Paused) allowed = false;
        if (DrawLocked) allowed = false;
        UpdateCursor(allowed);
        if (!allowed)
        {
            holding = false;
            drawFinger = -1;
            if (points.Count > 0) Finish();
            return;
        }

        ReadPointer(out Vector2 screenPos, out bool began, out bool ended, out bool held);

        // MP: a mouse click on a UI button must not start a stroke (touch already checks this)
        if (began && !(MPMatchManager.Instance != null && OverUI(screenPos))) holding = true;

        if (erasing && (MPMatchManager.Instance != null || tutorialMode))
        {
            if (points.Count > 0) Finish();
            if (holding && held)
            {
                Tip = ToWorld(screenPos);
                EraseAt(Tip);
            }
            if (ended)
            {
                holding = false;
                if (erasedThisHold) { erasedThisHold = false; EraseDone?.Invoke(); }
            }
            return;
        }

        if (holding && held)
        {
            Vector2 m = ToWorld(screenPos);

            if (points.Count == 0)
            {
                // not drawing yet: start as soon as the cursor is over a drawable spot
                if (player.health > 0f && Time.time >= nextStrokeTime && CanDraw(m)) StartStroke(m);
            }
            else
            {
                AudioManager.Draw(m);
                Tip = m;
                float d = Vector2.Distance(points[points.Count - 1], m);
                if (d >= minPointDistance)
                {
                    if (!SegmentDrawable(points[points.Count - 1], m)) Finish();   // left the drawable area (re-entering starts a new stroke)
                    else if (Pay(d)) AddPoint(m);
                    else Finish();                                                 // out of chalk
                }
            }
        }

        if (ended)
        {
            holding = false;
            if (points.Count > 0) Finish();
        }
    }

    // split-board MP: drawing is free during the drawing phase
    bool Pay(float distance)
    {
        float cost = distance * costPerUnit;
        if (MPDrawingPhase)
        {
            // limited chalk in the drawing phase (amount set on MPMatchManager), erasing refunds it
            return MPMatchManager.Instance.TrySpendDrawChalk(cost);
        }
        return player.Spend(cost);
    }

    void EraseAt(Vector2 p)
    {
        if (tutorialMode)
        {
            var tm = TutorialManager.Instance;
            if (tm && !tm.DusterReady) return;
            for (int i = ChalkStroke.All.Count - 1; i >= 0; i--)
            {
                var s = ChalkStroke.All[i];
                if (!s || s.DistanceTo(p) > eraseRadius) continue;
                Destroy(s.gameObject);
                if (tm) tm.StartDusterCooldown();
                erasedThisHold = true;      // fires EraseDone on release, which switches back to chalk
                break;
            }
            return;
        }

        if (MPDrawingPhase)
        {
            // drawing phase: erase my own strokes, get the chalk back
            for (int i = mine.Count - 1; i >= 0; i--)
            {
                var s = mine[i];
                if (!s) { mine.RemoveAt(i); continue; }
                if (s.DistanceTo(p) > eraseRadius) continue;

                mine.RemoveAt(i);
                MPMatchManager.Instance.RefundDrawChalk(s.ChalkCost);
                Destroy(s.gameObject);      // NetworkPlayer notices the missing stroke and tells the opponent
                erasedThisHold = true;
            }
            return;
        }

        // race: erase one stroke drawn during the race (mine or the opponent's), never the drawing-phase ones,
        // then the eraser has to recharge
        var m = MPMatchManager.Instance;
        if (!m.RaceEraseReady) return;
        for (int i = ChalkStroke.All.Count - 1; i >= 0; i--)
        {
            var s = ChalkStroke.All[i];
            if (!s || s.IsPermanent) continue;
            if (s.DistanceTo(p) <= eraseRadius)
            {
                NetworkPlayer.EraseRaceStroke(s);
                m.StartRaceEraseCooldown();
                erasedThisHold = true;      // switch to chalk when LMB is released
                break;
            }
        }
    }

    // ---- unified pointer: first free finger that starts off-UI, or the mouse ----
    void ReadPointer(out Vector2 pos, out bool began, out bool ended, out bool held)
    {
        pos = Vector2.zero; began = ended = held = false;

        if (Input.touchCount > 0 || drawFinger != -1)
        {
            pointerIsTouch = true;

            if (drawFinger == -1)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.phase != TouchPhase.Began) continue;
                    if (!cam.pixelRect.Contains(t.position)) continue;   // started in the black bars
                    if (OverUI(t.position)) continue;   // started on joystick / button
                    drawFinger = t.fingerId;
                    pos = t.position; began = held = true;
                    return;
                }
                return;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId != drawFinger) continue;
                pos = t.position;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { ended = true; drawFinger = -1; }
                else held = true;
                return;
            }
            // finger vanished (app switch, etc.)
            ended = true; drawFinger = -1;
            return;
        }

        pointerIsTouch = false;
        pos = Input.mousePosition;
        began = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        ended = Input.GetMouseButtonUp(0);
    }

    // Explicit UI raycast. IsPointerOverGameObject(fingerId) is unreliable on the frame a touch begins
    // (the EventSystem may not have processed that touch yet), which let drawing start on the joystick.
    static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    static bool OverUI(Vector2 screenPos)
    {
        if (!EventSystem.current) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        uiHits.Clear();
        EventSystem.current.RaycastAll(ped, uiHits);
        return uiHits.Count > 0;
    }

    Vector2 ToWorld(Vector2 screen)
    {
        if (pointerIsTouch && touchOffsetMm > 0f)
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            screen.y += touchOffsetMm * dpi / 25.4f;
        }
        return cam.ScreenToWorldPoint(screen);
    }

    void StartStroke(Vector2 start)
    {
        // wetness is locked in when the stroke starts
        lifetimeForThisStroke = baseLifetime * (1f + wetDurabilityBonus * player.Wetness);
        strokeColor = PlayerProfile.ChalkColor;
        preview.startColor = preview.endColor = strokeColor;
        StrokeBegan?.Invoke(strokeColor);
        AddPoint(start);
        AudioManager.Draw(start);
        Tip = start;
    }

    void AddPoint(Vector2 p)
    {
        points.Add(p);
        preview.positionCount = points.Count;
        preview.SetPosition(points.Count - 1, p);
        PointAdded?.Invoke(p);
    }

    void Finish()
    {
        if (points.Count == 0) return;
        if (points.Count >= 2)
        {
            BuildStroke();
            if (MPMatchManager.Instance != null && !MPDrawingPhase)
                nextStrokeTime = Time.time + strokeCooldown;
        }
        points.Clear();
        preview.positionCount = 0;
        StrokeEnded?.Invoke();
    }

    void BuildStroke()
    {
        bool mp = MPDrawingPhase;                      // drawing phase: chosen mode, permanent. Race: old rules (auto static, dissolves)
        bool dynamicStroke = mp ? dynamicMode
            : autoStatic
                ? !(IsAnchored(points[0]) && IsAnchored(points[points.Count - 1]))
                : strokesAreDynamic;

        var go = new GameObject("ChalkStroke");
        go.layer = LayerMask.NameToLayer(strokeLayerName);
        var stroke = go.AddComponent<ChalkStroke>();
        stroke.Init(points.ToArray(), lifetimeForThisStroke, waterMask,
                    waterDissolveMultiplier, lineWidth, dynamicStroke, strokeColor);

        if (mp) { stroke.SetFrozen(true); stroke.SetPermanent(true); }     // wait for the race / never dissolve
        stroke.ChalkCost = stroke.Length * costPerUnit;
        mine.Add(stroke);

        LastStroke = stroke;
        StrokeCreated?.Invoke(points.ToArray(), lifetimeForThisStroke, dynamicStroke, strokeColor);
    }

    // an end is anchored if it touches something that will not move (static collider or static stroke)
    bool IsAnchored(Vector2 p)
    {
        foreach (var c in Physics2D.OverlapCircleAll(p, anchorRadius, anchorMask))
        {
            if (c.isTrigger) continue;
            var rb = c.attachedRigidbody;
            if (rb == null || rb.bodyType == RigidbodyType2D.Static) return true;
        }
        return false;
    }

    bool CanDraw(Vector2 p)
    {
        if (MPBoard.Instance && MPDrawingPhase) return MPBoard.Instance.CanDraw(p);

        if (playerBorderRadius > 0f && player)
        {
            if (!bodyCol)
                foreach (var c in player.GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) { bodyCol = c; break; }
            if (bodyCol && Vector2.Distance(p, bodyCol.ClosestPoint(p)) <= playerBorderRadius) return false;
        }

        return Physics2D.OverlapPoint(p, drawableMask) && !Physics2D.OverlapPoint(p, blockedMask);
    }

    // checks every step between two points so a fast pointer can't skip over another layer
    bool SegmentDrawable(Vector2 a, Vector2 b)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (minPointDistance * 0.5f)));
        for (int i = 1; i <= steps; i++)
            if (!CanDraw(Vector2.Lerp(a, b, (float)i / steps))) return false;
        return true;
    }

    public ChalkStroke SpawnStroke(Vector2[] pts, float life, bool dyn, Color col)
    {
        var go = new GameObject("ChalkStroke");
        go.layer = LayerMask.NameToLayer(strokeLayerName);
        var s = go.AddComponent<ChalkStroke>();
        s.Init(pts, life, waterMask, waterDissolveMultiplier, lineWidth, dyn, col);
        return s;
    }

    LineRenderer CreateLine(GameObject go)
    {
        var lr = go.AddComponent<LineRenderer>();
        ChalkVisuals.Apply(lr, lineWidth);
        lr.startWidth = lr.endWidth = lineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.startColor = lr.endColor = Color.white;
        lr.sortingOrder = 10;
        lr.positionCount = 0;
        return lr;
    }

    void UpdateCursor(bool active)
    {
        if (Application.isMobilePlatform) return;
        int want = 0;
        if (active && !OverUI(Input.mousePosition))
            want = (erasing && (MPMatchManager.Instance != null || tutorialMode)) ? 2 : 1;

        Vector2 hot = want == 1 ? chalkHotspot : want == 2 ? eraserHotspot : Vector2.zero;
        if (want == cursorState && hot == appliedHotspot) return;   // re-applies when you tweak the hotspot
        cursorState = want;
        appliedHotspot = hot;

        if (want == 1 && chalkCursor) Cursor.SetCursor(chalkCursor, chalkHotspot, CursorMode.Auto);
        else if (want == 2 && eraserCursor) Cursor.SetCursor(eraserCursor, eraserHotspot, CursorMode.Auto);
        else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    void OnDisable() { cursorState = -1; if (!Application.isMobilePlatform) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
}