using UnityEngine;

// Tilt the phone (like a steering wheel) to move. Right edge down = move right, left edge down = move left.
// Put on any always-active object (e.g. UIRoot). Mobile builds only.
public class MobileTiltMove : MonoBehaviour
{
    public bool enableTilt = true;
    public float deadzoneDeg = 6f;       // tilt smaller than this = stand still
    public float fullTiltDeg = 30f;      // tilt at/after this = full speed
    public bool analog = true;           // false = full speed once past the dead zone
    public float smoothing = 15f;        // higher = snappier
    public bool invert = false;          // tick if left/right feels reversed

    [Header("Calibration")]
    public bool calibrateOnStart = true; // current hold angle becomes "neutral"
    public float calibrateDelay = 0.5f;

    [Header("Editor testing")]
    public bool showDebug;

    float neutralDeg, smoothed;
    bool calibrated;
    bool wasOn = true;

    void Start()
    {
#if !(UNITY_ANDROID || UNITY_IOS)
        enabled = false;
        return;
#endif
        if (!calibrateOnStart) calibrated = true;
    }

    void OnDisable() { MobileInput.Tilt = 0f; }

    // hook to a button if you want players to recalibrate
    public void Calibrate()
    {
        neutralDeg = RawTiltDeg();
        calibrated = true;
    }

    // degrees of "steering wheel" tilt: + = right edge down (in the screen's own left/right)
    float RawTiltDeg()
    {
        Vector3 a = Input.acceleration;
        float alongScreenRight;
        switch (Screen.orientation)
        {
            case ScreenOrientation.LandscapeLeft:  alongScreenRight = -a.y; break;
            case ScreenOrientation.LandscapeRight: alongScreenRight =  a.y; break;
            case ScreenOrientation.PortraitUpsideDown: alongScreenRight = -a.x; break;
            default: alongScreenRight = a.x; break;   // portrait
        }
        // acceleration is the "up" vector: right edge down makes it negative along screen-right
        float deg = -Mathf.Asin(Mathf.Clamp(alongScreenRight, -1f, 1f)) * Mathf.Rad2Deg;
        return invert ? -deg : deg;
    }

    void Update()
    {
        bool on = GameSettings.TiltEnabled;
        if (on && !wasOn) calibrated = false;    // recalibrate when switched back on
        wasOn = on;

        if (!enableTilt || !on || !GameManager.IsPlaying || MPMatchManager.LocalPaused)
        {
            smoothed = 0f;
            MobileInput.Tilt = 0f;
            return;
        }

        if (!calibrated)
        {
            if (Time.unscaledTime >= calibrateDelay) Calibrate();
            return;
        }

        float deg = RawTiltDeg() - neutralDeg;
        float mag = Mathf.Abs(deg);
        float k = GameSettings.Scale(GameSettings.TiltSensitivity);
        float dz = Mathf.Max(2f, deadzoneDeg * k);
        float full = Mathf.Clamp(fullTiltDeg * k, dz + 4f, 60f);

        float target = 0f;
        if (mag > dz)
            target = analog ? Mathf.Sign(deg) * Mathf.InverseLerp(dz, full, mag)
                            : Mathf.Sign(deg);

        smoothed = Mathf.Lerp(smoothed, target, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
        MobileInput.Tilt = smoothed;

        if (showDebug) Debug.Log("Tilt deg: " + deg.ToString("0.0") + "  out: " + smoothed.ToString("0.00"));
    }
}