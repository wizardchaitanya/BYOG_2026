using System;
using UnityEngine;

// Saved settings. Higher sensitivity = easier to trigger.
public static class GameSettings
{
    public static event Action Changed;

    static bool loaded;
    static float master = 1f, music = 0.5f, sfx = 1f, jump = 0.5f, tilt = 0.5f;
    static bool tiltOn = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { loaded = false; Changed = null; }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        master = PlayerPrefs.GetFloat("set_master", 1f);
        music  = PlayerPrefs.GetFloat("set_music", 0.5f);
        sfx    = PlayerPrefs.GetFloat("set_sfx", 1f);
        jump   = PlayerPrefs.GetFloat("set_jump", 0.5f);
        tilt   = PlayerPrefs.GetFloat("set_tilt", 0.5f);
        tiltOn = PlayerPrefs.GetInt("set_tilt_on", 1) == 1;
    }

    static void Set(ref float field, string key, float v)
    {
        Load();
        field = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(key, field);
        Changed?.Invoke();
    }

    public static float Master { get { Load(); return master; } set { Set(ref master, "set_master", value); } }
    public static float Music  { get { Load(); return music; }  set { Set(ref music, "set_music", value); } }
    public static float Sfx    { get { Load(); return sfx; }    set { Set(ref sfx, "set_sfx", value); } }
    public static float JumpSensitivity { get { Load(); return jump; } set { Set(ref jump, "set_jump", value); } }
    public static float TiltSensitivity { get { Load(); return tilt; } set { Set(ref tilt, "set_tilt", value); } }

    public static bool TiltEnabled
    {
        get { Load(); return tiltOn; }
        set { Load(); tiltOn = value; PlayerPrefs.SetInt("set_tilt_on", value ? 1 : 0); Changed?.Invoke(); }
    }

    // slider 0..1 -> threshold multiplier: 0 = x3 (hard), 0.5 = x1 (default), 1 = x0.33 (easy)
    public static float Scale(float s) { return Mathf.Pow(3f, 1f - 2f * s); }

    public static void Save() { PlayerPrefs.Save(); }
}