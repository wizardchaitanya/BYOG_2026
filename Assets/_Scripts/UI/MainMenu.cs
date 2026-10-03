using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Main menu scene: Play, Levels (level select), Quit.
// Level buttons are generated from Build Settings: every scene after index 0 is a level.
public class MainMenu : MonoBehaviour
{
    public string firstLevelSceneName = "Level1";   // used by the Play button

    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject levelsPanel;

    [Header("Level select")]
    public LevelButton levelButtonPrefab;
    public Transform levelButtonContainer;          // add a Grid Layout Group to it
    public bool unlockAllLevels = false;            // tick for testing

    void Start()
    {
        Time.timeScale = 1f;
        ShowMain();
    }

    public void Play() { AudioManager.UIClick(); SceneManager.LoadScene(firstLevelSceneName); }

    public void ShowMain()
    {
        mainPanel.SetActive(true);
        levelsPanel.SetActive(false);
    }

    // for the Back button (plays a click; ShowMain itself is silent because Start calls it)
    public void Back() { AudioManager.UIClick(); ShowMain(); }

    public void ShowLevels()
    {
        AudioManager.UIClick();
        mainPanel.SetActive(false);
        levelsPanel.SetActive(true);
        BuildLevelButtons();
    }

    public void Quit()
    {
        AudioManager.UIClick();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void BuildLevelButtons()
    {
        foreach (Transform child in levelButtonContainer) Destroy(child.gameObject);

        int count = SceneManager.sceneCountInBuildSettings;
        bool previousDone = true;   // level 1 is always open

        for (int i = 1; i < count; i++)
        {
            string sceneName = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            float best = PlayerPrefs.GetFloat("best_score_" + sceneName, 0f);

            var btn = Instantiate(levelButtonPrefab, levelButtonContainer);
            btn.Setup(i, i, best, unlockAllLevels || previousDone);

            previousDone = best > 0f;   // next level opens once this one has been completed
        }
    }
}