using UnityEngine;
using UnityEngine.EventSystems;

// Put on a UI Image/Button object (Raycast Target on). Pick the kind in the Inspector.
// Each finger is its own pointer, so holding Left + pressing Jump + drawing all work together.
public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum Kind { Left, Right, Jump, Pause }
    public Kind kind;

    bool down;

    public void OnPointerDown(PointerEventData e)
    {
        down = true;
        switch (kind)
        {
            case Kind.Left:  MobileInput.Left = true; break;
            case Kind.Right: MobileInput.Right = true; break;
            case Kind.Jump:  MobileInput.PressJump(); break;
            case Kind.Pause:
                if (GameManager.Instance) GameManager.Instance.Pause();
                else if (MPMatchManager.Instance) MPMatchManager.Instance.Pause();
                break;
        }
    }

    public void OnPointerUp(PointerEventData e) { Release(); }
    public void OnPointerExit(PointerEventData e) { Release(); }   // finger slid off
    void OnDisable() { Release(); }

    void Release()
    {
        if (!down) return;
        down = false;
        if (kind == Kind.Left) MobileInput.Left = false;
        else if (kind == Kind.Right) MobileInput.Right = false;
    }
}