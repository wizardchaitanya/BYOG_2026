using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drawing-phase panel: timer + Static / Dynamic / Erase tools + Done button.
// Put it on the Canvas (inside UIRoot). Panel is only visible during the drawing phase.
public class MPDrawUI : MonoBehaviour
{
    public MPMatchManager match;             // optional, found automatically
    public ChalkDrawer drawer;               // optional: leave empty if the drawer sits on the player prefab

    public GameObject panel;                 // everything that should show only while drawing
    public TMP_Text timerText;

    [Header("Tools")]
    public Button staticButton;
    public Button dynamicButton;
    public Button eraseButton;
    public Color activeColor = new Color(1f, 0.92f, 0.4f);
    public Color idleColor = Color.white;

    [Header("Race eraser (shown during the race)")]
    public Button raceEraseButton;
    public KeyCode raceEraseKey = KeyCode.E;

    [Header("Done")]
    public Button doneButton;
    public TMP_Text doneText;                // optional
    public string doneLabel = "DONE";
    public string waitingLabel = "WAITING...\n(tap to undo)";

    enum Tool { Static, Dynamic, Erase }
    Tool tool = Tool.Static;

    void Start()
    {
        if (!match) match = MPMatchManager.Instance;
        if (match)
        {
            match.PhaseChanged += OnPhase;
            OnPhase(match.Phase);
        }
        else if (panel) panel.SetActive(false);

        if (staticButton) staticButton.onClick.AddListener(() => Pick(Tool.Static));
        if (dynamicButton) dynamicButton.onClick.AddListener(() => Pick(Tool.Dynamic));
        if (eraseButton) eraseButton.onClick.AddListener(() => Pick(Tool.Erase));
        if (raceEraseButton) raceEraseButton.onClick.AddListener(ToggleRaceErase);
        if (doneButton) doneButton.onClick.AddListener(() => { if (match) match.RequestDone(); });
    }

    void OnDestroy() { if (match) match.PhaseChanged -= OnPhase; }

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
            // only the control for the active end mode is shown
            if (timerText) timerText.gameObject.SetActive(match.UsesTimer);
            if (doneButton) doneButton.gameObject.SetActive(match.UsesButton);
        }
    }

    void ToggleRaceErase()
    {
        if (!match || match.Phase != MPPhase.Racing || MPMatchManager.LocalPaused) return;
        var d = D;
        if (!d) return;
        AudioManager.UIClick();
        d.SetErasing(!d.erasing);
        Tint(raceEraseButton, d.erasing);
    }

    void Pick(Tool t)
    {
        AudioManager.UIClick();
        tool = t;
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
            d.SetErasing(tool == Tool.Erase);
            if (tool != Tool.Erase) d.SetDynamic(tool == Tool.Dynamic);
        }
        Tint(staticButton, tool == Tool.Static);
        Tint(dynamicButton, tool == Tool.Dynamic);
        Tint(eraseButton, tool == Tool.Erase);
    }

    void Tint(Button b, bool on)
    {
        if (b && b.targetGraphic) b.targetGraphic.color = on ? activeColor : idleColor;
    }

    float nextApply;

    void Update()
    {
        if (match && match.Phase == MPPhase.Racing && Input.GetKeyDown(raceEraseKey)) ToggleRaceErase();
        if (!match || match.Phase != MPPhase.Drawing) return;

        if (timerText) timerText.text = Mathf.CeilToInt(match.DrawTimeLeft).ToString();
        if (doneText) doneText.text = match.IsLocalDone ? waitingLabel : doneLabel;

        // the drawer may only exist after the player prefab spawns: keep re-applying the tool for a moment
        if (Time.unscaledTime >= nextApply) { nextApply = Time.unscaledTime + 0.5f; Apply(); }
    }
}