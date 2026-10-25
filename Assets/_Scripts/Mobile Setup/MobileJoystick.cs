using UnityEngine;
using UnityEngine.EventSystems;

// Virtual joystick. Left/right = move, push up = jump.
// Setup: put this on the joystick BACKGROUND Image (Raycast Target on, bottom-left, inside the SafeArea panel).
// Assign a child Image as `handle` (Raycast Target off). Both anchored/pivoted at center.
public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform handle;
    [Range(0.05f, 0.6f)] public float deadzone = 0.25f;     // horizontal dead zone (fraction of radius)
    [Range(0.3f, 0.95f)] public float jumpThreshold = 0.6f; // push up past this = jump
    [Range(0.1f, 0.8f)] public float jumpReset = 0.35f;     // must drop below this before the next jump
    public bool analog = false;                             // false = full speed once past dead zone

    RectTransform area;
    bool jumpArmed = true;
    bool active;

    void Awake() { area = (RectTransform)transform; }

    public void OnPointerDown(PointerEventData e) { active = true; OnDrag(e); }

    public void OnDrag(PointerEventData e)
    {
        if (!active) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out Vector2 local)) return;

        // local is relative to the rect's PIVOT; subtract the rect center so any pivot works
        Vector2 center = area.rect.center;
        float radius = Mathf.Max(1f, area.rect.width * 0.5f);
        Vector2 v = Vector2.ClampMagnitude((local - center) / radius, 1f);

        if (handle) handle.localPosition = center + v * radius;

        // horizontal
        float x = 0f;
        if (Mathf.Abs(v.x) > deadzone)
            x = analog ? Mathf.Sign(v.x) * Mathf.InverseLerp(deadzone, 1f, Mathf.Abs(v.x)) : Mathf.Sign(v.x);
        MobileInput.Axis = x;

        // jump: fires once per push, re-arms when the stick comes back down
        if (jumpArmed && v.y > jumpThreshold) { MobileInput.PressJump(); jumpArmed = false; }
        else if (v.y < jumpReset) jumpArmed = true;
    }

    public void OnPointerUp(PointerEventData e) { Release(); }
    void OnDisable() { Release(); }

    void Release()
    {
        active = false;
        jumpArmed = true;
        MobileInput.Axis = 0f;
        if (handle && area) handle.localPosition = area.rect.center;
    }
}