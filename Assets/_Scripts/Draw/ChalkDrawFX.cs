using UnityEngine;

// Chalk dust that sprays from the tip of the chalk while the player is drawing.
// Add next to ChalkDrawer.
[RequireComponent(typeof(ChalkDrawer))]
public class ChalkDrawFX : MonoBehaviour
{
    public float idleRate = 10f;         // particles/sec while drawing (even when the mouse is still)
    public float distanceRate = 25f;     // extra particles per world unit drawn
    public int sortingOrder = 11;        // above strokes (5) and the preview line (10)

    ChalkDrawer drawer;
    ParticleSystem ps;
    Material mat;
    bool wasDrawing;

    void Awake()
    {
        drawer = GetComponent<ChalkDrawer>();
        ps = Build();
    }

    void OnDestroy()
    {
        if (ps) Destroy(ps.gameObject);
        if (mat) Destroy(mat);
    }

    void LateUpdate()
    {
        bool drawing = drawer.IsDrawing;
        var em = ps.emission;

        if (drawing)
        {
            if (!wasDrawing)
            {
                var c = PlayerProfile.ChalkColor;
                var main = ps.main;
                main.startColor = new Color(c.r, c.g, c.b, 0.9f);   // dust matches the chalk colour
            }
            ps.transform.position = drawer.Tip;
            em.rateOverTime = idleRate;
            // skip the first frame so the jump from the old position doesn't leave a trail
            em.rateOverDistance = wasDrawing ? distanceRate : 0f;
        }
        else
        {
            em.rateOverTime = 0f;
            em.rateOverDistance = 0f;
        }
        wasDrawing = drawing;
    }

    ParticleSystem Build()
    {
        var go = new GameObject("ChalkDrawDust");
        var p = go.AddComponent<ParticleSystem>();
        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = p.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
        main.gravityModifier = 0.4f;          // dust settles downward
        main.maxParticles = 150;

        var em = p.emission;
        em.rateOverTime = 0f;
        em.rateOverDistance = 0f;

        var shape = p.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;

        var fade = p.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var shrink = p.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = ChalkVisuals.Dot();

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = sortingOrder;

        p.Play();
        return p;
    }
}