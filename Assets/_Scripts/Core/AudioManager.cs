using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent audio: background music (crossfades between scenes), SFX pool, and a drawing loop.
// Put the SAME AudioManager object in every scene (on a ROOT object). The first one survives scene
// loads, duplicates delete themselves, so you can also press Play from any level.
// Everything is optional: leave a clip empty and that sound is simply skipped.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Music")]
    public AudioClip menuMusic;                 // build index 0
    public AudioClip[] levelMusic;              // level 1 = [0], level 2 = [1] ... wraps around
    public float musicFade = 1.5f;
    [Range(0f, 1f)] public float pauseDuck = 0.3f;

    [Header("Player")]
    public AudioClip[] footsteps;
    public AudioClip jump;
    public AudioClip land;

    [Header("Drawing (looping chalk scratch)")]
    public AudioClip drawLoop;                  // set Loop-friendly clip
    [Range(0f, 1f)] public float drawMaxVolume = 0.7f;
    public float drawMinPitch = 0.85f;
    public float drawMaxPitch = 1.25f;
    public float drawFullSpeed = 8f;            // mouse speed (units/sec) that gives full volume

    [Header("World")]
    public AudioClip strokeBreak;
    public AudioClip crusherSlam;
    public float hearDistance = 25f;            // world sounds fade out over this distance from the camera

    [Header("Game")]
    public AudioClip death;
    public AudioClip levelComplete;
    public AudioClip uiClick;

    AudioSource[] music = new AudioSource[2];
    float[] fade = new float[2];
    int active;
    AudioClip currentMusic;
    float duck = 1f, duckTarget = 1f;

    AudioSource[] pool = new AudioSource[8];
    int poolIndex;

    AudioSource drawSource;
    float drawUntil, drawIntensity, lastDrawTime = -10f;
    Vector2 lastDrawPos;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < music.Length; i++)
        {
            music[i] = NewSource(true);
            music[i].volume = 0f;
        }
        for (int i = 0; i < pool.Length; i++) pool[i] = NewSource(false);

        drawSource = NewSource(true);
        drawSource.clip = drawLoop;
        drawSource.volume = 0f;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        if (Instance == this) ChooseMusic(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        return s;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        duckTarget = 1f;
        ChooseMusic(scene);
    }

    void ChooseMusic(Scene scene)
    {
        int idx = scene.buildIndex;
        AudioClip clip = null;
        if (idx <= 0) clip = menuMusic;
        else if (levelMusic != null && levelMusic.Length > 0) clip = levelMusic[(idx - 1) % levelMusic.Length];
        PlayMusic(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == currentMusic) return;      // same track keeps playing across restarts
        currentMusic = clip;
        active = 1 - active;
        var s = music[active];
        s.clip = clip;
        s.volume = 0f;
        fade[active] = 0f;
        if (clip) s.Play();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;     // works while the game is paused
        duck = Mathf.MoveTowards(duck, duckTarget, dt * 3f);

        for (int i = 0; i < music.Length; i++)
        {
            float target = (i == active && music[i].clip) ? 1f : 0f;
            fade[i] = Mathf.MoveTowards(fade[i], target, dt / Mathf.Max(0.01f, musicFade));
            music[i].volume = fade[i] * musicVolume * masterVolume * duck;
            if (fade[i] <= 0f && target == 0f && music[i].isPlaying) music[i].Stop();
        }

        if (drawLoop)
        {
            bool drawing = Time.unscaledTime < drawUntil;
            float targetVol = drawing ? drawIntensity * drawMaxVolume * sfxVolume * masterVolume : 0f;
            drawSource.volume = Mathf.MoveTowards(drawSource.volume, targetVol, dt * 4f);
            drawSource.pitch = Mathf.Lerp(drawMinPitch, drawMaxPitch, drawIntensity);

            if (drawSource.volume > 0.001f && !drawSource.isPlaying) drawSource.Play();
            else if (drawSource.volume <= 0.001f && drawSource.isPlaying) drawSource.Pause();
        }
    }

    void PlayClip(AudioClip clip, float volume, float pitchVariation)
    {
        if (!clip) return;
        var s = pool[poolIndex];
        poolIndex = (poolIndex + 1) % pool.Length;
        s.clip = clip;
        s.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        s.volume = volume * sfxVolume * masterVolume;
        s.Play();
    }

    void DrawTick(Vector2 pos)
    {
        float t = Time.unscaledTime;
        float speed = 0f;
        if (t - lastDrawTime < 0.2f)
            speed = Vector2.Distance(pos, lastDrawPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
        lastDrawPos = pos;
        lastDrawTime = t;

        drawIntensity = Mathf.Lerp(drawIntensity, Mathf.Clamp01(speed / drawFullSpeed), 0.3f);
        drawUntil = t + 0.1f;                  // loop fades out by itself when calls stop
    }

    // ---- static helpers: safe to call even when there is no AudioManager in the scene ----
    public static void Jump() { if (Instance) Instance.PlayClip(Instance.jump, 0.8f, 0.05f); }
    public static void Land() { if (Instance) Instance.PlayClip(Instance.land, 0.8f, 0.08f); }

    public static void Footstep()
    {
        if (Instance && Instance.footsteps != null && Instance.footsteps.Length > 0)
            Instance.PlayClip(Instance.footsteps[Random.Range(0, Instance.footsteps.Length)], 0.5f, 0.1f);
    }

    public static void Draw(Vector2 worldPos) { if (Instance) Instance.DrawTick(worldPos); }
    public static void StrokeBreak() { if (Instance) Instance.PlayClip(Instance.strokeBreak, 0.9f, 0.1f); }
    public static void UIClick() { if (Instance) Instance.PlayClip(Instance.uiClick, 0.8f, 0.02f); }

    public static void CrusherSlam(Vector2 pos)
    {
        if (!Instance) return;
        float d = Camera.main ? Vector2.Distance(Camera.main.transform.position, pos) : 0f;
        float v = Mathf.Clamp01(1f - d / Instance.hearDistance);
        if (v > 0.01f) Instance.PlayClip(Instance.crusherSlam, v, 0.05f);
    }

    public static void OnGameState(GameState s)
    {
        if (!Instance) return;
        switch (s)
        {
            case GameState.Playing:  Instance.duckTarget = 1f; break;
            case GameState.Paused:   Instance.duckTarget = Instance.pauseDuck; break;
            case GameState.Dead:     Instance.duckTarget = 0.4f; Instance.PlayClip(Instance.death, 1f, 0f); break;
            case GameState.Complete: Instance.duckTarget = 0.5f; Instance.PlayClip(Instance.levelComplete, 1f, 0f); break;
        }
    }
}