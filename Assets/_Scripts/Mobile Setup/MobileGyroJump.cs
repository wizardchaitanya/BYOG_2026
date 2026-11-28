using UnityEngine;

// Flick the phone = jump. Put on any always-active object (e.g. UIRoot). Mobile builds only.
public class MobileGyroJump : MonoBehaviour
{
    public bool enableGyroJump = true;
    public float flickThreshold = 6f;       // rotation speed (rad/s) that counts as a flick. Lower = more sensitive
    public float cooldown = 0.35f;          // min seconds between jumps
    public bool useAccelFallback = true;    // devices without a gyro: use a sudden acceleration spike
    public float accelThreshold = 1.8f;     // g-force change that counts as a flick (fallback only)

    [Header("Editor testing")]
    public bool showDebug;

    float nextAllowed;
    Vector3 lastAccel;
    bool hasGyro;

    void Start()
    {
#if !(UNITY_ANDROID || UNITY_IOS)
        enabled = false;
        return;
#endif
        hasGyro = SystemInfo.supportsGyroscope;
        if (hasGyro) Input.gyro.enabled = true;
        lastAccel = Input.acceleration;
    }

    void OnDisable()
    {
        if (SystemInfo.supportsGyroscope) Input.gyro.enabled = false;
    }

    void Update()
    {
        if (!enableGyroJump) return;

        bool flick = false;
        float value = 0f;
        float k = GameSettings.Scale(GameSettings.JumpSensitivity);

        if (hasGyro)
        {
            value = Input.gyro.rotationRateUnbiased.magnitude;
            flick = value > flickThreshold * k;
        }
        else if (useAccelFallback)
        {
            Vector3 a = Input.acceleration;
            value = (a - lastAccel).magnitude / Mathf.Max(Time.unscaledDeltaTime, 0.001f) * 0.02f;
            lastAccel = a;
            flick = (a - lastAccel).magnitude > accelThreshold * k || value > accelThreshold * k;
        }

        if (showDebug && value > 0.5f) Debug.Log("Flick value: " + value);

        if (!flick || Time.unscaledTime < nextAllowed) return;
        if (!GameManager.IsPlaying || MPMatchManager.LocalPaused) return;

        nextAllowed = Time.unscaledTime + cooldown;
        MobileInput.PressJump();
    }
}