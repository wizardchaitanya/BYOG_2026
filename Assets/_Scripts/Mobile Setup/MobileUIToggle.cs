using UnityEngine;

// Shows the mobile controls only in Android / iOS builds. Windows, Mac, Linux and WebGL (HTML) builds hide them.
// Put it on any always-active object (e.g. UIRoot) and drag the mobile controls into `mobileOnly`:
// joystick, jump button, pause button, etc. Do NOT list the HUD, panels or MP draw UI here.
public class MobileUIToggle : MonoBehaviour
{
    public GameObject[] mobileOnly; // drag the mobile controls here

    [Header("Editor testing")]
    public bool showInEditor = false;       // tick to preview the mobile UI while playing in the editor

    void Awake()
    {
        bool show;
#if UNITY_ANDROID || UNITY_IOS
        show = true;
#else
        show = false;
#endif
#if UNITY_EDITOR
        show = showInEditor;
#endif
        foreach (var go in mobileOnly)
            if (go) go.SetActive(show);

        if (!show) MobileInput.Clear();     // make sure no stale touch input is left behind
    }
}