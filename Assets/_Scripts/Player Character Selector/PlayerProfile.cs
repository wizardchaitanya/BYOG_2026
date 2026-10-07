using UnityEngine;

// Saved player choices: which character, which chalk colour. Persists between sessions.
public static class PlayerProfile
{
    const string CharKey = "profile_character";
    const string ColorKey = "profile_chalk_color";

    static bool loaded;
    static int character;
    static Color color = Color.white;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        character = PlayerPrefs.GetInt(CharKey, 0);
        if (!ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString(ColorKey, "FFFFFF"), out color))
            color = Color.white;
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
}
