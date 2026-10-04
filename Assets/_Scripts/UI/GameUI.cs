using System.Collections;
using TMPro;
using UnityEngine;

// HUD timer + pause / death / level complete panels. Buttons call GameManager methods
// (Resume, Restart, NextLevel, LoadMenu) through their OnClick events.
public class GameUI : MonoBehaviour
{
    [Header("HUD")]
    public GameObject hudRoot;
    public TMP_Text timerText;

    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject deathPanel;
    public GameObject completePanel;

    [Header("Death")]
    public TMP_Text deathReasonText;

    [Header("Level complete")]
    public TMP_Text timeText;
    public TMP_Text chalkText;
    public TMP_Text scoreText;
    public TMP_Text bestText;
    public GameObject nextButton;            // hidden on the last level
    public float scoreCountTime = 1.2f;

    GameManager gm;

    void Start()
    {
        gm = GameManager.Instance;
        if (!gm) { Debug.LogError("GameUI: no GameManager in scene", this); enabled = false; return; }
        gm.StateChanged += Apply;
        Apply(gm.State);
    }

    void OnDestroy() { if (gm) gm.StateChanged -= Apply; }

    void Update()
    {
        if (timerText) timerText.text = GameManager.FormatTime(gm.Timer);
    }

    void Apply(GameState s)
    {
        Show(hudRoot, s == GameState.Playing || s == GameState.Paused);
        Show(pausePanel, s == GameState.Paused);
        Show(deathPanel, s == GameState.Dead);
        Show(completePanel, s == GameState.Complete);

        if (s == GameState.Dead && deathReasonText) deathReasonText.text = gm.DeathReason;
        if (s == GameState.Complete) ShowComplete();
    }

    void ShowComplete()
    {
        if (timeText) timeText.text = "Time  " + GameManager.FormatTime(gm.Timer);
        if (chalkText) chalkText.text = "Chalk left  " + Mathf.RoundToInt(gm.HealthScore * 100f) + "%";
        if (bestText) bestText.text = gm.NewBest
            ? "New best!"
            : "Best  " + gm.BestScore.ToString("0.0") + " / 10   " /*+ GameManager.FormatTime(gm.BestTime)*/;
        Show(nextButton, gm.HasNextLevel);
        StartCoroutine(CountUp());
    }

    IEnumerator CountUp()
    {
        float t = 0f;
        while (t < scoreCountTime)
        {
            t += Time.unscaledDeltaTime;
            float v = Mathf.Lerp(0f, gm.FinalScore, t / scoreCountTime);
            if (scoreText) scoreText.text = v.ToString("0.0") + " / 10";
            yield return null;
        }
        if (scoreText) scoreText.text = gm.FinalScore.ToString("0.0") + " / 10";
    }

    static void Show(GameObject go, bool on) { if (go) go.SetActive(on); }
}
