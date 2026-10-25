using UnityEngine;

// Replaces CameraFit AND SafeArea. Put on the orthographic camera of every level.
// The game view is ALWAYS the same aspect (designAspect), centered inside the phone's safe area and scaled
// to the largest size that fits. All game UI lives in `uiRoot`, which is sized and scaled to exactly
// match that view. So view + UI are identical on every phone; only the scale changes.
// Anything left over is black bars (empty, no UI there).
//
// Setup:
//  - Canvas: Render Mode Overlay, Canvas Scaler = Constant Pixel Size (scale factor 1).
//  - Under the Canvas create an empty RectTransform "UIRoot". Put ALL HUD/joystick/buttons/panels inside it.
//  - Remove CameraFit and SafeArea components. Assign uiRoot here.
//  - Camera's Orthographic Size = the size you designed for this level (read once at start).
[RequireComponent(typeof(Camera))]
public class FixedAspectView : MonoBehaviour
{
    public RectTransform uiRoot;
    public float designAspect = 16f / 9f;     // aspect of your designed view (same for every level)
    public float referenceHeight = 1080f;     // UI is laid out in a (referenceHeight * aspect) x referenceHeight space
    public bool useSafeArea = true;

    [Header("Borders (% of screen, live-editable in Play mode)")]
    [Range(0f, 40f)] public float borderLeft;
    [Range(0f, 40f)] public float borderRight;
    [Range(0f, 40f)] public float borderTop;
    [Range(0f, 40f)] public float borderBottom;

    [Header("Where the view sits in the leftover space (0 = left/bottom, 0.5 = center, 1 = right/top)")]
    [Range(0f, 1f)] public float alignX = 0.5f;
    [Range(0f, 1f)] public float alignY = 0.5f;

    Vector4 lastBorders;
    Vector2 lastAlign;
    float lastAspect;

    Camera cam, bg;
    Canvas canvas;
    float designSize;
    int lastW, lastH;
    Rect lastSafe;
    float lastScale;

    void Awake()
    {
        cam = GetComponent<Camera>();
        designSize = cam.orthographicSize;
        if (uiRoot) canvas = uiRoot.GetComponentInParent<Canvas>().rootCanvas;

        var go = new GameObject("LetterboxBG");
        bg = go.AddComponent<Camera>();
        bg.depth = cam.depth - 100f;
        bg.clearFlags = CameraClearFlags.SolidColor;
        bg.backgroundColor = Color.black;
        bg.cullingMask = 0;
        bg.orthographic = true;

        Apply();
    }

    void OnDestroy() { if (bg) Destroy(bg.gameObject); }

    void LateUpdate()
    {
        float sf = canvas ? canvas.scaleFactor : 1f;
        var borders = new Vector4(borderLeft, borderRight, borderTop, borderBottom);
        var align = new Vector2(alignX, alignY);
        if (Screen.width != lastW || Screen.height != lastH || Screen.safeArea != lastSafe || !Mathf.Approximately(sf, lastScale)
            || borders != lastBorders || align != lastAlign || !Mathf.Approximately(designAspect, lastAspect))
            Apply();
    }

    void Apply()
    {
        lastW = Screen.width; lastH = Screen.height; lastSafe = Screen.safeArea;
        if (lastW <= 0 || lastH <= 0) return;

        lastBorders = new Vector4(borderLeft, borderRight, borderTop, borderBottom);
        lastAlign = new Vector2(alignX, alignY);
        lastAspect = designAspect;

        Rect full = useSafeArea ? Screen.safeArea : new Rect(0, 0, lastW, lastH);

        // manual borders shrink the usable area first
        Rect area = Rect.MinMaxRect(
            full.xMin + lastW * borderLeft / 100f,
            full.yMin + lastH * borderBottom / 100f,
            full.xMax - lastW * borderRight / 100f,
            full.yMax - lastH * borderTop / 100f);
        if (area.width < 50f || area.height < 50f) area = full;   // safety: borders too big

        // largest designAspect rectangle that fits in the area, placed by alignX / alignY
        float w = area.width, h = area.height;
        if (w / h > designAspect) w = h * designAspect; else h = w / designAspect;
        Rect view = new Rect(area.x + (area.width - w) * alignX, area.y + (area.height - h) * alignY, w, h);

        cam.rect = new Rect(view.x / lastW, view.y / lastH, view.width / lastW, view.height / lastH);
        cam.orthographicSize = designSize;      // never changes

        if (uiRoot)
        {
            float sf = canvas ? canvas.scaleFactor : 1f;
            lastScale = sf;
            uiRoot.anchorMin = uiRoot.anchorMax = uiRoot.pivot = new Vector2(0.5f, 0.5f);
            uiRoot.sizeDelta = new Vector2(referenceHeight * designAspect, referenceHeight);
            uiRoot.localScale = Vector3.one * (view.height / (referenceHeight * sf));
            Vector2 offset = view.center - new Vector2(lastW, lastH) * 0.5f;
            uiRoot.anchoredPosition = offset / sf;
        }
    }
}