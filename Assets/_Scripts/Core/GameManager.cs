using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public enum GameState { Playing, Paused, Dead, Complete }

// One per level scene. Owns the timer, state, scoring and scene flow.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    // other scripts check this; true when there is no manager in the scene
    public static bool IsPlaying => Instance != null ? Instance.State == GameState.Playing : MPMatchManager.CanPlay;

    public ChalkPlayer player;

    [Header("Scoring (out of 10)")]
    public float parTime = 30f;              // finish at or under this = full time score
    public float slowTime = 90f;             // finish at or over this = zero time score
    [Range(0f, 1f)] public float timeWeight = 0.5f;   // rest of the score comes from chalk left

    [Header("Scenes")]
    public string menuSceneName = "MainMenu";

    public GameState State { get; private set; } = GameState.Playing;
    public float Timer { get; private set; }
    public string DeathReason { get; private set; }

    // results of a completed level
    public float TimeScore { get; private set; }
    public float HealthScore { get; private set; }
    public float FinalScore { get; private set; }
    public bool NewBest { get; private set; }
    public float BestScore { get; private set; }
    public float BestTime { get; private set; }

    public event Action<GameState> StateChanged;

    string ScoreKey => "best_score_" + SceneManager.GetActiveScene().name;
    string TimeKey => "best_time_" + SceneManager.GetActiveScene().name;

    public bool HasNextLevel
    {
        get
        {
            int n = SceneManager.GetActiveScene().buildIndex + 1;
            if (n >= SceneManager.sceneCountInBuildSettings) return false;
            return !Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(n)).StartsWith("MP_");
        }
    }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (!player) player = FindObjectOfType<ChalkPlayer>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (State == GameState.Playing) Timer += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        Time.timeScale = 0f;
        SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        AudioManager.UIClick();
        Time.timeScale = 1f;
        SetState(GameState.Playing);
    }

    public void PlayerDied(string reason)
    {
        if (State != GameState.Playing) return;
        DeathReason = reason;
        SetState(GameState.Dead);
    }

    public void CompleteLevel()
    {
        if (State != GameState.Playing) return;

        TimeScore = Mathf.Clamp01(Mathf.InverseLerp(slowTime, parTime, Timer));
        HealthScore = player ? Mathf.Clamp01(player.health / player.maxHealth) : 0f;
        FinalScore = Mathf.Round(10f * (timeWeight * TimeScore + (1f - timeWeight) * HealthScore) * 10f) / 10f;

        float oldScore = PlayerPrefs.GetFloat(ScoreKey, 0f);
        float oldTime = PlayerPrefs.GetFloat(TimeKey, float.MaxValue);
        NewBest = FinalScore > oldScore;
        BestScore = Mathf.Max(oldScore, FinalScore);
        BestTime = Mathf.Min(oldTime, Timer);
        PlayerPrefs.SetFloat(ScoreKey, BestScore);
        PlayerPrefs.SetFloat(TimeKey, BestTime);
        PlayerPrefs.Save();

        SetState(GameState.Complete);
    }

    // button hooks
    public void Restart()
    {
        AudioManager.UIClick();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        AudioManager.UIClick();
        Time.timeScale = 1f;
        if (HasNextLevel) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        else LoadMenu();
    }

    public void LoadMenu()
    {
        AudioManager.UIClick();
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    void SetState(GameState s)
    {
        State = s;
        StateChanged?.Invoke(s);
        AudioManager.OnGameState(s);
    }

    public static string FormatTime(float t)
    {
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return string.Format("{0:00}:{1:00}", m, s);
    }
}