using UnityEngine;

// Put on "UIRoot" (empty RectTransform directly under the Canvas). Put ALL mobile UI inside it.
// Canvas: Render Mode Overlay, Canvas Scaler = Constant Pixel Size, Scale Factor 1.
// Layout is designed once at referenceWidth x referenceHeight and scaled uniformly to every phone.
// Don't use together with FixedAspectView on the same UIRoot.
[RequireComponent(typeof(RectTransform))]
public class UIFitRoot : MonoBehaviour
{
    public enum Mode
    {
        FitInside,     // layout identical everywhere; leftover space on one axis stays empty
        MatchHeight    // scale from height; root widens/narrows, so corner-anchored UI hugs the screen corners
    }

    public Mode mode = Mode.MatchHeight;
    public float referenceWidth = 1920f;
    public float referenceHeight = 1080f;
    public bool useSafeArea = true;

    RectTransform rt;
    Canvas canvas;
    int lastW, lastH;
    Rect lastSafe;
    float lastSf;
    Mode lastMode;

    void Awake()
    {
        rt = (RectTransform)transform;
        var c = GetComponentInParent<Canvas>();
        if (c) canvas = c.rootCanvas;
        Apply();
    }

    void LateUpdate()
    {
        float sf = canvas ? canvas.scaleFactor : 1f;
        if (Screen.width != lastW || Screen.height != lastH || Screen.safeArea != lastSafe
            || !Mathf.Approximately(sf, lastSf) || mode != lastMode)
            Apply();
    }

    void Apply()
    {
        lastW = Screen.width; lastH = Screen.height; lastSafe = Screen.safeArea; lastMode = mode;
        if (lastW <= 0 || lastH <= 0) return;

        float sf = canvas ? canvas.scaleFactor : 1f;
        lastSf = sf;

        Rect area = useSafeArea ? Screen.safeArea : new Rect(0, 0, lastW, lastH);

        // pixels per reference unit
        float s = mode == Mode.FitInside
            ? Mathf.Min(area.width / referenceWidth, area.height / referenceHeight)
            : area.height / referenceHeight;

        Vector2 size = mode == Mode.FitInside
            ? new Vector2(referenceWidth, referenceHeight)
            : new Vector2(area.width / s, referenceHeight);

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * (s / sf);
        rt.anchoredPosition = (area.center - new Vector2(lastW, lastH) * 0.5f) / sf;
    }
}