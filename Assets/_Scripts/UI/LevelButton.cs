using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Prefab for one level-select button.
// Needs: Button on the root, a TMP_Text for the number, optional TMP_Text for best score, optional lock icon.
[RequireComponent(typeof(Button))]
public class LevelButton : MonoBehaviour
{
    public TMP_Text numberText;
    public TMP_Text bestText;
    public GameObject lockIcon;

    Button button;
    int buildIndex;

    public void Setup(int levelNumber, int sceneBuildIndex, float bestScore, bool unlocked)
    {
        button = GetComponent<Button>();
        buildIndex = sceneBuildIndex;

        if (numberText) numberText.text = levelNumber.ToString();
        if (bestText) bestText.text = bestScore > 0f ? bestScore.ToString("0.0") + " / 10" : "";
        if (lockIcon) lockIcon.SetActive(!unlocked);

        button.interactable = unlocked;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => { AudioManager.UIClick(); SceneManager.LoadScene(buildIndex); });
    }
}