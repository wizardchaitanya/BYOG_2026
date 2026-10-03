using UnityEngine;

// Per-level rain control. Put one in each scene.
// Other scripts read RainManager.Intensity (0 = dry, 1 = heavy). No manager in scene = full rain.
public class RainManager : MonoBehaviour
{
    public enum Mode { Constant, Pulse, Random }

    public static RainManager Instance { get; private set; }
    public static float Intensity => Instance ? Instance.Current : 1f;
    public static bool IsRaining => Intensity > 0.01f;

    [Header("Level setting")]
    public bool rainEnabled = true;          // untick for levels without rain
    public Mode mode = Mode.Constant;
    [Range(0f, 1f)] public float baseIntensity = 0.6f;   // Constant mode
    public float changeSpeed = 0.4f;         // how fast intensity eases to its target

    [Header("Pulse mode (storm waves)")]
    [Range(0f, 1f)] public float minIntensity = 0.2f;
    [Range(0f, 1f)] public float maxIntensity = 1f;
    public float pulsePeriod = 20f;          // seconds per wave

    [Header("Random mode")]
    public Vector2 changeInterval = new Vector2(8f, 15f);

    [Header("Effects (optional)")]
    public ParticleSystem rainParticles;
    public float maxEmission = 300f;         // particles/sec at intensity 1
    public AudioSource rainAudio;
    public float maxVolume = 0.6f;

    public float Current { get; private set; }

    float randomTarget, nextChange;

    void Awake()
    {
        Instance = this;
        Current = Target();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        Current = Mathf.MoveTowards(Current, Target(), changeSpeed * Time.deltaTime);
        ApplyEffects();
    }

    float Target()
    {
        if (!rainEnabled) return 0f;

        switch (mode)
        {
            case Mode.Pulse:
                float wave = (Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, pulsePeriod)) + 1f) * 0.5f;
                return Mathf.Lerp(minIntensity, maxIntensity, wave);

            case Mode.Random:
                if (Time.time >= nextChange)
                {
                    randomTarget = Random.Range(minIntensity, maxIntensity);
                    nextChange = Time.time + Random.Range(changeInterval.x, changeInterval.y);
                }
                return randomTarget;

            default:
                return baseIntensity;
        }
    }

    void ApplyEffects()
    {
        if (rainParticles)
        {
            var em = rainParticles.emission;
            em.rateOverTime = maxEmission * Current;
            if (Current > 0.01f && !rainParticles.isPlaying) rainParticles.Play();
            else if (Current <= 0.01f && rainParticles.isPlaying)
                rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (rainAudio)
        {
            rainAudio.volume = maxVolume * Current;
            if (Current > 0.01f && !rainAudio.isPlaying) rainAudio.Play();
            else if (Current <= 0.01f && rainAudio.isPlaying) rainAudio.Stop();
        }
    }

    // for scripted events (storm starts, trigger zones, etc.)
    public void SetRainEnabled(bool on) => rainEnabled = on;
    public void SetBaseIntensity(float value) { baseIntensity = Mathf.Clamp01(value); mode = Mode.Constant; }
}
