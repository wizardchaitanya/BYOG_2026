using UnityEngine;
using UnityEngine.UI;

// Chalk powder bar. Assign an Image set to Type = Filled (Horizontal).
public class ChalkHealthUI : MonoBehaviour
{
    public ChalkPlayer player;
    public Image fill;                     // the bar itself (Image Type: Filled)
    public Image trail;                    // optional: slower bar behind, shows recent loss
    public Gradient color = DefaultGradient();
    public float fillSpeed = 10f;
    public float trailSpeed = 1.5f;

    float shown = 1f, trailShown = 1f;

    void Update()
    {
        if (!player || !fill) return;

        float target = Mathf.Clamp01(player.health / player.maxHealth);
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-fillSpeed * Time.deltaTime));
        fill.fillAmount = shown;
        fill.color = color.Evaluate(shown);

        if (trail)
        {
            trailShown = trailShown > target
                ? Mathf.MoveTowards(trailShown, target, trailSpeed * Time.deltaTime)
                : target;
            trail.fillAmount = trailShown;
        }
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.9f, 0.25f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.5f),
                new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
