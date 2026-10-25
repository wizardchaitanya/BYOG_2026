using UnityEngine;

// Put on the orthographic camera of each level. Set the camera's Orthographic Size to what you designed.
// - Screen WIDER than designAspect: size stays EXACTLY as designed, extra width is revealed at the sides
//   (extend your background art past the playable area so it never looks empty).
// - Screen NARROWER than designAspect: zooms out just enough to keep the full designed width visible.
// - Beyond [minAspect, maxAspect]: black bars, so nobody sees unlimited extra map.
// Multiplayer fairness: tick lockExactView -> everyone gets exactly the designed view (bars if needed).
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    public float designAspect = 16f / 9f;   // aspect you built this level's view on
    public float minAspect = 16f / 10f;     // narrower than this -> bars top/bottom
    public float maxAspect = 19.5f / 9f;    // wider than this -> bars left/right (caps the extra width)
    public bool lockExactView = false;      // true = designed view only, bars on any other aspect

    Camera cam, bg;
    float designSize, designWidth;
    int lastW, lastH;

    void Awake()
    {
        cam = GetComponent<Camera>();
        designSize = cam.orthographicSize;
        designWidth = designSize * 2f * designAspect;

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

    void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH) Apply();
    }

    void Apply()
    {
        lastW = Screen.width; lastH = Screen.height;
        if (lastH <= 0) return;

        float screenAspect = (float)lastW / lastH;
        float lo = lockExactView ? designAspect : minAspect;
        float hi = lockExactView ? designAspect : maxAspect;
        float a = Mathf.Clamp(screenAspect, lo, hi);

        Rect r = new Rect(0f, 0f, 1f, 1f);
        if (screenAspect > a) { r.width = a / screenAspect; r.x = (1f - r.width) * 0.5f; }
        else if (screenAspect < a) { r.height = screenAspect / a; r.y = (1f - r.height) * 0.5f; }
        cam.rect = r;

        // wider than design: keep height (size never changes). narrower: keep designed width.
        cam.orthographicSize = a >= designAspect ? designSize : designWidth * 0.5f / a;
    }
}