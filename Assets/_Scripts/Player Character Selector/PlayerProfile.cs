using UnityEngine;

// Saved player choices: name, which character, which chalk colour. Persists between sessions.
public static class PlayerProfile
{
    const string CharKey = "profile_character";
    const string ColorKey = "profile_chalk_color";
    const string NameKey = "profile_name";
    const string TutKey = "profile_tutorial_done";

    static bool loaded;
    static int character;
    static Color color = Color.white;
    static string playerName = "";

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        character = PlayerPrefs.GetInt(CharKey, 0);
        if (!ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString(ColorKey, "FFFFFF"), out color))
            color = Color.white;
        playerName = PlayerPrefs.GetString(NameKey, "");
    }

    public static int CharacterIndex
    {
        get { Load(); return character; }
        set
        {
            Load();
            character = Mathf.Max(0, value);
            PlayerPrefs.SetInt(CharKey, character);
            PlayerPrefs.Save();
        }
    }

    // colour of the drawn chalk lines
    public static Color ChalkColor
    {
        get { Load(); return color; }
        set
        {
            Load();
            color = new Color(value.r, value.g, value.b, 1f);
            PlayerPrefs.SetString(ColorKey, ColorUtility.ToHtmlStringRGB(color));
            PlayerPrefs.Save();
        }
    }

    // ---- player name ----
    public static bool HasName { get { Load(); return !string.IsNullOrEmpty(playerName); } }

    public static string PlayerName
    {
        get { Load(); return string.IsNullOrEmpty(playerName) ? "Player" : playerName; }
        set
        {
            Load();
            playerName = Clean(value);
            PlayerPrefs.SetString(NameKey, playerName);
            PlayerPrefs.Save();
        }
    }

    public static string Clean(string s)
    {
        s = (s ?? "").Trim();
        return s.Length > 16 ? s.Substring(0, 16) : s;
    }
    
    public static bool TutorialDone
    {
        get { return PlayerPrefs.GetInt(TutKey, 0) == 1; }
        set { PlayerPrefs.SetInt(TutKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}