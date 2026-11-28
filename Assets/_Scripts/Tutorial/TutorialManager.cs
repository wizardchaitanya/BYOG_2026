using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

// One per tutorial scene. Runs the steps in order; each step ends on its goal (event), then the next begins.
public class TutorialManager : MonoBehaviour
{
    public enum Goal { ReachZone, Jump, DrawSeconds, Wait, DrawDynamic, DrawStatic, Erase }

    [System.Serializable]
    public class Step
    {
        public string name;
        public GameObject panel;        // texts / arrows shown during this step
        public Goal goal;
        public TutorialZone zone;       // ReachZone
        public int count = 2;           // Jump: how many jumps
        public float seconds = 5f;      // DrawSeconds / Wait
        public bool allowDraw;          // false = drawing locked in this step
        public bool showDuster;         // show the duster button
        public GameObject hideOnDraw;   // hidden as soon as the player starts drawing (sample doodles)
        public float delayAfter = 0.8f; // pause before the next step
        public UnityEvent onStart;
    }

    public ChalkDrawer drawer;
    public ChalkPlayer player;
    public Step[] steps;
    public GameObject dusterButton;     // OnClick -> ToggleDuster
    public GameObject finishArrow;      // OnClick -> Finish. Shown after the last step
    public GameObject pausePanel;      // Resume button -> Resume(), Skip button -> Finish()
    public string menuSceneName = "MainMenu";
    public float fallY = -15f;          // fell off the map: back to start

    [Header("Duster")]
    public float dusterCooldown = 3f;
    public KeyCode dusterKey = KeyCode.E;
    public Color activeColor = new Color(1f, 0.92f, 0.4f);   // button colour while the duster is selected
    public Image dusterCooldownFill;       // optional: radial recharge overlay
    public TMP_Text dusterCooldownText;    // optional: seconds left
    public Color cooldownColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);

    Button dusterBtn;
    Color dusterIdle = Color.white;
    float dusterReadyAt;
    static Sprite white;

    public bool DusterReady => Time.time >= dusterReadyAt;
    public float DusterWait => Mathf.Max(0f, dusterReadyAt - Time.time);
    public void StartDusterCooldown() { dusterReadyAt = Time.time + dusterCooldown; }

    int index = -1, progress;
    float timer;
    bool locked;
    Vector3 startPos;
    Rigidbody2D rb;

    Step Cur => index >= 0 && index < steps.Length ? steps[index] : null;

    public static TutorialManager Instance { get; private set; }
    public static bool Paused { get; private set; }

    void Start()
    {
        Instance = this;
        Paused = false;
        if (pausePanel) pausePanel.SetActive(false);

        Time.timeScale = 1f;
        rb = player.GetComponent<Rigidbody2D>();
        startPos = player.transform.position;

        drawer.tutorialMode = true;
        drawer.StrokeCreated += OnStroke;
        drawer.EraseDone += OnErase;
        ChalkPlayerController.Jumped += OnJump;

        foreach (var s in steps) if (s.panel) s.panel.SetActive(false);
        if (dusterButton) dusterButton.SetActive(false);
        if (dusterButton)
        {
            dusterBtn = dusterButton.GetComponent<Button>();
            if (dusterBtn && dusterBtn.targetGraphic) dusterIdle = dusterBtn.targetGraphic.color;   // original colour
        }
        if (dusterCooldownFill)
        {
            var f = dusterCooldownFill;
            if (!f.sprite)
            {
                if (!white)
                {
                    var t = new Texture2D(1, 1); t.SetPixel(0, 0, Color.white); t.Apply();
                    white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                }
                f.sprite = white;
            }
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Radial360;
            f.fillOrigin = (int)Image.Origin360.Top;
            f.fillClockwise = false;
            f.raycastTarget = false;
            f.color = cooldownColor;
            f.enabled = false;
        }
        if (finishArrow) finishArrow.SetActive(false);
        Begin(0);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Paused = false;
        Time.timeScale = 1f;

        if (drawer) { drawer.StrokeCreated -= OnStroke; drawer.EraseDone -= OnErase; }
        ChalkPlayerController.Jumped -= OnJump;
        ChalkDrawer.DrawLocked = false;
    }

    void Begin(int i)
    {
        var old = Cur;
        if (old != null)
        {
            if (old.zone) old.zone.Entered -= OnZone;
            if (old.panel) old.panel.SetActive(false);
        }

        index = i; locked = false; progress = 0; timer = 0f;

        if (i >= steps.Length)
        {
            ChalkDrawer.DrawLocked = false;
            drawer.SetErasing(false);
            if (finishArrow) finishArrow.SetActive(true);
            return;
        }

        var s = steps[i];
        if (s.panel) s.panel.SetActive(true);
        if (s.hideOnDraw) s.hideOnDraw.SetActive(true);
        if (s.zone) s.zone.Entered += OnZone;
        ChalkDrawer.DrawLocked = !s.allowDraw;
        drawer.SetErasing(false);
        TintDuster(false);
        if (dusterButton) dusterButton.SetActive(s.showDuster);
        s.onStart?.Invoke();
    }

    void Complete()
    {
        if (locked || Cur == null) return;
        locked = true;
        StartCoroutine(NextAfter(Cur.delayAfter));
    }

    IEnumerator NextAfter(float d)
    {
        yield return new WaitForSeconds(d);
        Begin(index + 1);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (Paused) Resume(); else Pause();
        }
        if (Paused) return;
        UpdateDuster();

        if (player && player.transform.position.y < fallY)
        {
            player.transform.position = startPos;
            if (rb) rb.velocity = Vector2.zero;
        }

        var s = Cur;
        if (s == null || locked) return;

        if (s.hideOnDraw && drawer.IsDrawing) s.hideOnDraw.SetActive(false);

        if (s.goal == Goal.DrawSeconds)
        {
            if (drawer.IsDrawing) timer += Time.deltaTime;
            if (timer >= s.seconds) Complete();
        }
        else if (s.goal == Goal.Wait)
        {
            timer += Time.deltaTime;
            if (timer >= s.seconds) Complete();
        }
    }

    // ---------- events ----------
    void OnZone() { if (Cur != null && Cur.goal == Goal.ReachZone) Complete(); }

    void OnJump()
    {
        if (Cur == null || locked || Cur.goal != Goal.Jump) return;
        if (++progress >= Cur.count) Complete();
    }

    void OnStroke(Vector2[] pts, float life, bool dynamic, Color c)
    {
        if (Cur == null || locked) return;
        if (Cur.goal == Goal.DrawDynamic && dynamic) Complete();
        else if (Cur.goal == Goal.DrawStatic && !dynamic) Complete();
    }

    void OnErase()
    {
        drawer.SetErasing(false);
        TintDuster(false);
        if (Cur != null && Cur.goal == Goal.Erase) Complete();
    }

    // ---------- button hooks ----------
    public void ToggleDuster()
    {
        if (Paused) return;
        // can't switch ON during the cooldown (switching off is still allowed)
        if (!drawer.erasing && !DusterReady) return;
        AudioManager.UIClick();
        drawer.SetErasing(!drawer.erasing);
        TintDuster(drawer.erasing);
    }

    public void Finish()     // arrow button (also usable for a Skip button)
    {
        AudioManager.UIClick();
        PlayerProfile.TutorialDone = true;
        Paused = false; Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    public void Pause()
    {
        if (Paused) return;
        AudioManager.UIClick();
        Paused = true;
        Time.timeScale = 0f;
        if (pausePanel) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (!Paused) return;
        AudioManager.UIClick();
        Paused = false;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
    }

    void UpdateDuster()
    {
        if (!dusterButton || !dusterButton.activeInHierarchy) return;

        if (Input.GetKeyDown(dusterKey)) ToggleDuster();

        float wait = DusterWait;
        if (dusterBtn) dusterBtn.interactable = wait <= 0f || drawer.erasing;
        if (dusterCooldownFill)
        {
            dusterCooldownFill.enabled = wait > 0.001f;
            dusterCooldownFill.fillAmount = wait / Mathf.Max(0.01f, dusterCooldown);
        }
        if (dusterCooldownText) dusterCooldownText.text = wait > 0f ? Mathf.CeilToInt(wait).ToString() : "";
    }

    void TintDuster(bool on)
    {
        if (dusterBtn && dusterBtn.targetGraphic)
            dusterBtn.targetGraphic.color = on ? activeColor : dusterIdle;
    }
}