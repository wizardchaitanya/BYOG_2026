using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drawing-phase UI: timer + one Chalk/Eraser toggle. Dynamic drawing is disabled for now (static only).
// The fog over the opponent's half carries the instruction text + Done button.
// Player 1 (left half) sees the fog panel on the RIGHT, player 2 sees it on the LEFT.
public class MPDrawUI : MonoBehaviour
{
    public MPMatchManager match;             // optional, found automatically
    public ChalkDrawer drawer;               // optional: leave empty if the drawer sits on the player prefab

    public GameObject panel;                 // shown only while drawing (timer + tool button)
    public TMP_Text timerText;

    [Header("Tool toggle (Chalk <-> Eraser)")]
    public Button toolButton;
    public Image toolIcon;                   // optional: swaps between the two sprites
    public Sprite chalkSprite;               // shown while drawing
    public Sprite eraserSprite;              // shown while the eraser is active
    public Color activeColor = new Color(1f, 0.92f, 0.4f);   // button colour while erasing
    public Color idleColor = Color.white;

    [Header("Fog side panels (place each over its half of the board, under UIRoot)")]
    public FogSidePanel leftFogPanel;        // used when I'm the RIGHT player (fog on the left)
    public FogSidePanel rightFogPanel;       // used when I'm the LEFT player (fog on the right)
    [TextArea] public string instruction = "Draw your path on the {0} half.\nPress DONE when you're ready.";
    [TextArea] public string instructionTimer = "Draw your path on the {0} half.\nThe race starts when time runs out.";

    [Header("Race eraser (shown during the race)")]
    public Button raceEraseButton;
    public KeyCode raceEraseKey = KeyCode.E;
    public Image raceEraseCooldownFill;      // optional: Image Type = Filled, shows the recharge
    public TMP_Text raceEraseCooldownText;   // optional: seconds left

    [Header("Done (optional extra button outside the fog)")]
    public Button doneButton;
    public KeyCode doneKey = KeyCode.Space;
    public TMP_Text doneText;                // optional
    public string doneLabel = "DONE";
    public string waitingLabel = "WAITING...\n(tap to undo)";

    enum Tool { Static, Erase }
    Tool tool = Tool.Static;

    public Color cooldownColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
    static Sprite white;

    ChalkDrawer hooked;
    FogSidePanel activeSide;

    void Start()
    {
        if (!match) match = MPMatchManager.Instance;
        if (match)
        {
            match.PhaseChanged += OnPhase;
            OnPhase(match.Phase);
        }
        else
        {
            if (panel) panel.SetActive(false);
            ShowSides(null);
        }

        if (toolButton) toolButton.onClick.AddListener(ToggleTool);
        if (raceEraseButton) raceEraseButton.onClick.AddListener(ToggleRaceErase);
        HookDone(doneButton);
        HookDone(leftFogPanel ? leftFogPanel.doneButton : null);
        HookDone(rightFogPanel ? rightFogPanel.doneButton : null);

        if (raceEraseCooldownFill)
        {
            var f = raceEraseCooldownFill;
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
            f.fillClockwise = false;          // shrinking amount = grey edge sweeps clockwise
            f.raycastTarget = false;
            f.color = cooldownColor;
            f.enabled = false;
        }
    }

    void HookDone(Button b)
    {
        if (b) b.onClick.AddListener(() => { if (match) match.RequestDone(); });
    }

    void OnDestroy()
    {
        if (match) match.PhaseChanged -= OnPhase;
        if (hooked) hooked.EraseDone -= BackToChalk;
    }

    void OnPhase(MPPhase p)
    {
        if (panel) panel.SetActive(p == MPPhase.Drawing);
        if (raceEraseButton) raceEraseButton.gameObject.SetActive(p == MPPhase.Racing);
        Tint(raceEraseButton, false);
        var drw = D;
        if (drw) drw.SetErasing(false);                 // every phase starts in draw mode

        if (p == MPPhase.Drawing)
        {
            tool = Tool.Static;
            Apply();
            if (timerText) timerText.gameObject.SetActive(match.UsesTimer);
            if (doneButton) doneButton.gameObject.SetActive(match.UsesButton);

            // fog is on the opponent's half: I'm left (index 0) -> fog on the right, and vice versa
            bool practice = MPBoard.Instance && MPBoard.Instance.practice;
            int mine = match.MyIndex;
            ShowSides(practice ? null : (mine == 0 ? rightFogPanel : leftFogPanel));

            if (activeSide)
            {
                string side = mine == 0 ? "LEFT" : "RIGHT";
                if (activeSide.instructionText)
                    activeSide.instructionText.text = string.Format(match.UsesTimer ? instructionTimer : instruction, side);
                if (activeSide.timerText) activeSide.timerText.gameObject.SetActive(match.UsesTimer);
                if (activeSide.doneButton) activeSide.doneButton.gameObject.SetActive(match.UsesButton);
            }
        }
        else ShowSides(null);
    }

    void ShowSides(FogSidePanel show)
    {
        activeSide = show;
        if (leftFogPanel) leftFogPanel.gameObject.SetActive(leftFogPanel == show);
        if (rightFogPanel) rightFogPanel.gameObject.SetActive(rightFogPanel == show);
    }

    void ToggleRaceErase()
    {
        if (!match || match.Phase != MPPhase.Racing || MPMatchManager.LocalPaused) return;
        var d = D;
        if (!d) return;

        // can't switch ON during the cooldown (switching off is still allowed)
        if (!d.erasing && !match.RaceEraseReady) return;

        AudioManager.UIClick();
        d.SetErasing(!d.erasing);
        Tint(raceEraseButton, d.erasing);
    }

    // one button: chalk <-> eraser
    void ToggleTool()
    {
        if (!match || match.Phase != MPPhase.Drawing || MPMatchManager.LocalPaused) return;
        AudioManager.UIClick();
        tool = tool == Tool.Erase ? Tool.Static : Tool.Erase;
        Apply();
    }

    ChalkDrawer D
    {
        get
        {
            if (drawer) return drawer;
            var l = NetworkPlayer.Local;
            return l ? l.Drawer : null;
        }
    }

    void Apply()
    {
        var d = D;
        if (d)
        {
            d.SetDynamic(false);                 // dynamic drawing is off for now
            d.SetErasing(tool == Tool.Erase);
        }
        Tint(toolButton, tool == Tool.Erase);
        if (toolIcon)
        {
            var s = tool == Tool.Erase ? eraserSprite : chalkSprite;
            if (s) toolIcon.sprite = s;
        }
    }

    void Tint(Button b, bool on)
    {
        if (b && b.targetGraphic) b.targetGraphic.color = on ? activeColor : idleColor;
    }

    float nextApply;

    void Update()
    {
        var cur = D;
        if (cur != hooked)
        {
            if (hooked) hooked.EraseDone -= BackToChalk;
            hooked = cur;
            if (cur) cur.EraseDone += BackToChalk;
        }

        if (match && match.Phase == MPPhase.Racing)
        {
            if (Input.GetKeyDown(raceEraseKey)) ToggleRaceErase();
            float wait = match.RaceEraseWait;
            if (raceEraseButton) raceEraseButton.interactable = wait <= 0f || (D && D.erasing);
            if (raceEraseCooldownFill)
            {
                raceEraseCooldownFill.enabled = wait > 0.001f;
                raceEraseCooldownFill.fillAmount = wait / Mathf.Max(0.01f, match.raceEraseCooldown);
            }
            if (raceEraseCooldownText) raceEraseCooldownText.text = wait > 0f ? Mathf.CeilToInt(wait).ToString() : "";
        }
        if (!match || match.Phase != MPPhase.Drawing) return;

        // E: toggle chalk <-> eraser
        if (Input.GetKeyDown(raceEraseKey) && !MPMatchManager.LocalPaused) ToggleTool();

        if (Input.GetKeyDown(doneKey) && !MPMatchManager.LocalPaused)
            match.RequestDone();

        string time = Mathf.CeilToInt(match.DrawTimeLeft).ToString();
        string label = match.IsLocalDone ? waitingLabel : doneLabel;
        if (timerText) timerText.text = time;
        if (doneText) doneText.text = label;
        if (activeSide)
        {
            if (activeSide.timerText) activeSide.timerText.text = time;
            if (activeSide.doneText) activeSide.doneText.text = label;
        }

        // the drawer may only exist after the player prefab spawns: keep re-applying the tool for a moment
        if (Time.unscaledTime >= nextApply) { nextApply = Time.unscaledTime + 0.5f; Apply(); }
    }

    // erased one stroke in the drawing phase: stay on the eraser. In the race: back to chalk.
    void BackToChalk()
    {
        if (!match) return;
        if (match.Phase == MPPhase.Drawing) { Apply(); }
        else if (match.Phase == MPPhase.Racing)
        {
            if (hooked) hooked.SetErasing(false);
            Tint(raceEraseButton, false);
        }
    }
}