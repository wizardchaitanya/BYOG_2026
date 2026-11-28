using UnityEngine;

// Written by MobileButton / MobileJoystick / MobileTiltMove, read by ChalkPlayerController.
public static class MobileInput
{
    public static bool Left, Right;
    static bool jumpFlag;

    public static float Axis;   // set by MobileJoystick (-1..1)
    public static float Tilt;   // set by MobileTiltMove (-1..1)

    public static float Horizontal => Mathf.Clamp(Axis + Tilt + (Right ? 1f : 0f) - (Left ? 1f : 0f), -1f, 1f);

    public static void PressJump() { jumpFlag = true; }

    // true once per press
    public static bool JumpPressed
    {
        get { bool j = jumpFlag; jumpFlag = false; return j; }
    }

    public static void Clear() { Left = Right = false; Axis = 0f; Tilt = 0f; jumpFlag = false; }
}