using UnityEngine;

// Player juice: leans while running + chalk dust from the feet (and a puff when landing).
// Add next to ChalkPlayerController. Uses the same child "Visual" object as ChalkBodyHealth.
[RequireComponent(typeof(ChalkPlayerController))]
public class ChalkPlayerFX : MonoBehaviour
{
    [Header("Tilt")]
    public Transform visual;                 // child with the SpriteRenderer (NOT the player root)
    public float maxTilt = 8f;               // degrees at full speed
    public float tiltSmooth = 12f;
    public bool leanBack = true;             // true = tilts to the opposite side of movement, false = leans forward

    [Header("Chalk dust")]
    public Transform feet;                   // defaults to the controller's GroundCheck
    public float runThreshold = 1.5f;        // min speed to kick up dust
    public float dustRate = 25f;             // particles/sec at full speed
    public int landingPuff = 8;
    public float minLandSpeed = 4f;          // fall speed needed for a landing puff

    ChalkPlayerController controller;
    Rigidbody2D rb;
    ParticleSystem dust;
    Material dustMat;
    Texture2D dotTex;
    float angle, airVy;
    bool wasGrounded;

    void Awake()
    {
        controller = GetComponent<ChalkPlayerController>();
        rb = GetComponent<Rigidbody2D>();
        if (!feet) feet = controller.groundCheck;
        if (visual == transform)
        {
            Debug.LogError("ChalkPlayerFX: Visual must be a CHILD object, not the player root. Tilt disabled.", this);
            visual = null;
        }
        dust = BuildDust();
    }

    void OnDestroy()
    {
        if (dust) Destroy(dust.gameObject);
        if (dustMat) Destroy(dustMat);
        if (dotTex) Destroy(dotTex);
    }

    void LateUpdate()
    {
        float vx = rb.velocity.x;
        float speed01 = Mathf.Clamp(vx / Mathf.Max(0.1f, controller.maxSpeed), -1f, 1f);

        // ---- tilt ----
        if (visual)
        {
            float target = (leanBack ? 1f : -1f) * maxTilt * speed01;
            angle = Mathf.Lerp(angle, target, 1f - Mathf.Exp(-tiltSmooth * Time.deltaTime));
            // the player root is mirrored when facing left, so flip the local angle to keep the world tilt right
            float mirror = Mathf.Sign(transform.lossyScale.x);
            visual.localRotation = Quaternion.Euler(0f, 0f, angle * mirror);
        }

        // ---- dust ----
        dust.transform.position = feet ? feet.position : transform.position;

        bool grounded = controller.Grounded;
        bool running = GameManager.IsPlaying && grounded && Mathf.Abs(vx) > runThreshold;

        var em = dust.emission;
        em.rateOverTime = running ? dustRate * Mathf.Abs(speed01) : 0f;

        if (running)
        {
            float dirX = Mathf.Sign(vx);
            // kick back and up, away from the way we are running
            dust.transform.rotation = Quaternion.LookRotation(new Vector3(-dirX * 0.8f, 0.6f, 0f));
        }

        // landing puff
        if (!grounded) airVy = rb.velocity.y;
        else if (!wasGrounded && airVy < -minLandSpeed)
        {
            dust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // straight up
            dust.Emit(landingPuff);
        }
        wasGrounded = grounded;
    }

    ParticleSystem BuildDust()
    {
        var go = new GameObject("ChalkDust");   // not parented, so the player's mirroring can't affect it

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new Color(0.95f, 0.95f, 0.95f, 0.8f);
        main.gravityModifier = -0.1f;           // drifts up a little, like dust
        main.maxParticles = 80;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.1f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var shrink = ps.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        dotTex = BuildDotTexture();
        dustMat = new Material(Shader.Find("Sprites/Default"));
        dustMat.mainTexture = dotTex;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dustMat;
        var sr = GetComponentInChildren<SpriteRenderer>();
        r.sortingLayerID = sr ? sr.sortingLayerID : 0;
        r.sortingOrder = sr ? sr.sortingOrder - 1 : 0;   // behind the player

        ps.Play();
        return ps;
    }

    static Texture2D BuildDotTexture()
    {
        const int s = 32;
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d));
            }
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        t.SetPixels(px);
        t.Apply();
        return t;
    }
}
