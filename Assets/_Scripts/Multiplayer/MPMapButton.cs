using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Prefab script: one map button with a name and a status line.
public class MPMapButton : MonoBehaviour
{
    public Button button;
    public TMP_Text nameText;
    public TMP_Text statusText;

    public void Setup(string title, System.Action onClick)
    {
        if (nameText) nameText.text = title;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
    }

    public void SetStatus(string s) { if (statusText) statusText.text = s; }
    public void SetInteractable(bool on) { button.interactable = on; }
}