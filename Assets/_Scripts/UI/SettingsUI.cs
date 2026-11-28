using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Settings panel. Works from the main menu and the pause panels: hook any Settings button to Open(), Back to Close().
public class SettingsUI : MonoBehaviour
{
    public GameObject panel;

    [Header("Volume")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Gyro (mobile)")]
    public GameObject mobileSection;        // everything gyro-related, hidden on PC
    public Slider jumpSensitivitySlider;    // flick-to-jump is always on, only its sensitivity is adjustable
    public Toggle tiltToggle;               // left / right tilt on / off
    public Slider tiltSensitivitySlider;

    [Header("Optional value labels")]
    public TMP_Text masterText, musicText, sfxText, jumpText, tiltText;

    void Awake()
    {
        Hook(masterSlider, v => GameSettings.Master = v);
        Hook(musicSlider, v => GameSettings.Music = v);
        Hook(sfxSlider, v => GameSettings.Sfx = v);
        Hook(jumpSensitivitySlider, v => GameSettings.JumpSensitivity = v);
        Hook(tiltSensitivitySlider, v => GameSettings.TiltSensitivity = v);
        if (tiltToggle) tiltToggle.onValueChanged.AddListener(v =>
        {
            AudioManager.UIClick();
            GameSettings.TiltEnabled = v;
            Refresh();
        });

        if (mobileSection) mobileSection.SetActive(Application.isMobilePlatform || Application.isEditor);
        if (panel && panel != gameObject) panel.SetActive(false);
    }

    void Hook(Slider s, System.Action<float> set)
    {
        if (!s) return;
        s.minValue = 0f; s.maxValue = 1f;
        s.onValueChanged.AddListener(v => { set(v); Refresh(); });
    }

    public void Open()
    {
        AudioManager.UIClick();
        Load();
        panel.SetActive(true);
    }

    public void Close()
    {
        AudioManager.UIClick();
        GameSettings.Save();
        panel.SetActive(false);
    }

    void OnDisable() { GameSettings.Save(); }

    void Load()
    {
        Set(masterSlider, GameSettings.Master);
        Set(musicSlider, GameSettings.Music);
        Set(sfxSlider, GameSettings.Sfx);
        Set(jumpSensitivitySlider, GameSettings.JumpSensitivity);
        Set(tiltSensitivitySlider, GameSettings.TiltSensitivity);
        if (tiltToggle) tiltToggle.SetIsOnWithoutNotify(GameSettings.TiltEnabled);
        Refresh();
    }

    static void Set(Slider s, float v) { if (s) s.SetValueWithoutNotify(v); }

    void Refresh()
    {
        Label(masterText, GameSettings.Master);
        Label(musicText, GameSettings.Music);
        Label(sfxText, GameSettings.Sfx);
        Label(jumpText, GameSettings.JumpSensitivity);
        Label(tiltText, GameSettings.TiltSensitivity);
        if (tiltSensitivitySlider) tiltSensitivitySlider.interactable = GameSettings.TiltEnabled;
    }

    static void Label(TMP_Text t, float v) { if (t) t.text = Mathf.RoundToInt(v * 100f) + "%"; }
}