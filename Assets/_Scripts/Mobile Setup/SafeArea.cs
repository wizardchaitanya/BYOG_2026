using UnityEngine;

// Put on a full-stretch panel directly under the Canvas. Put all HUD / buttons inside it.
// Keeps UI out of notches, rounded corners and gesture bars.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform rt;
    Rect lastArea;
    Vector2Int lastRes;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != lastArea || lastRes.x != Screen.width || lastRes.y != Screen.height)
            Apply();
    }

    void Apply()
    {
        lastArea = Screen.safeArea;
        lastRes = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = lastArea.position;
        Vector2 max = lastArea.position + lastArea.size;
        min.x /= Screen.width;  min.y /= Screen.height;
        max.x /= Screen.width;  max.y /= Screen.height;

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
