using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public enum MPPhase { Waiting, Drawing, Reveal, Racing, Finished }
public enum DrawEndMode { Timer, Button }   // how the drawing phase ends: only one at a time

// Split-board match: Waiting -> Drawing (timer / both press Done) -> Reveal (fog lifts, 3-2-1) -> Racing -> Finished.
// Needs a PhotonView on the same object (no observed components).
public class MPMatchManager : MonoBehaviourPunCallbacks
{
    public static MPMatchManager Instance { get; private set; }
    public static bool CanPlay => Instance == null || Instance.Phase == MPPhase.Racing;                  // players can move
    public static bool DrawAllowed => Instance == null || Instance.Phase == MPPhase.Racing ||
        (Instance.Phase == MPPhase.Drawing && !Instance.iDone && (!Instance.UsesTimer || Instance.DrawTimeLeft > 0f));
    public static bool LocalPaused => Instance != null && Instance.paused;                               // local menu open: my input is off, the match keeps running

    public string playerPrefabName = "MPPlayer";        // prefab inside a Resources folder
    public Transform[] spawnPoints;                      // both near the START point: [0] = player 1, [1] = player 2
    public string lobbySceneName = "MP_Lobby";
    public int countdownSeconds = 3;

    [Header("Drawing phase")]
    public DrawEndMode endMode = DrawEndMode.Timer;      // Timer: ends when time runs out. Button: ends when both players press Done.
    public float drawSeconds = 60f;                      // only used in Timer mode
    public float drawChalkLimit = 100f;                  // chalk available in the drawing phase (same units as ChalkDrawer cost: line length x Cost Per Unit). Erasing refunds it.
    public float flushDelay = 0.6f;

    [Header("Race")]
    public KeyCode resetKey = KeyCode.R;                 // back to the spawn point (also callable from a UI button: ResetMyPlayer)
    [Range(2f, 5f)] public float raceEraseCooldown = 3f; // seconds to wait before the eraser works again (race only)                      // wait after drawing ends so last strokes arrive before the reveal

    [Header("UI")]
    public TMP_Text countdownText;
    public GameObject resultPanel;
    public TMP_Text resultText;

    [Header("Pause (local only, the match keeps running)")]
    public GameObject pausePanel;         // Resume button -> Resume(), Leave button -> LeaveToLobby()

    [Header("Respawn UI")]
    public GameObject respawnPanel;
    public TMP_Text respawnText;

    [Header("Rematch")]
    public Button rematchButton;
    public TMP_Text rematchText;          // optional: "Waiting for opponent..."

    [Header("Hook your camera follow here (Transform = local player)")]
    public UnityEvent<Transform> onLocalPlayerSpawned;

    public MPPhase Phase { get; private set; } = MPPhase.Waiting;
    public event System.Action<MPPhase> PhaseChanged;

    // ---------- drawing-phase chalk (tracked here, not on the player) ----------
    public float DrawChalkLeft { get; private set; }

    public bool TrySpendDrawChalk(float cost)
    {
        if (DrawChalkLeft < cost) return false;
        DrawChalkLeft -= cost;
        SyncChalkBar();
        return true;
    }

    public void RefundDrawChalk(float amount)
    {
        DrawChalkLeft = Mathf.Min(drawChalkLimit, DrawChalkLeft + amount);
        SyncChalkBar();
    }

    // the normal chalk bar shows the remaining drawing chalk as a fraction
    void SyncChalkBar()
    {
        if (NetworkPlayer.Local) NetworkPlayer.Local.SetChalk(drawChalkLimit > 0f ? DrawChalkLeft / drawChalkLimit : 0f);
    }

    // ---------- race eraser cooldown ----------
    float eraseReadyAt;
    public bool RaceEraseReady => Time.time >= eraseReadyAt;
    public float RaceEraseWait => Mathf.Max(0f, eraseReadyAt - Time.time);
    public void StartRaceEraseCooldown() { eraseReadyAt = Time.time + raceEraseCooldown; }

    public void ResetMyPlayer()
    {
        if (Phase != MPPhase.Racing || paused) return;
        if (NetworkPlayer.Local) NetworkPlayer.Local.ResetToSpawn();
    }

    public bool UsesTimer => endMode == DrawEndMode.Timer;
    public bool UsesButton => endMode == DrawEndMode.Button;
    public bool Racing => Phase == MPPhase.Racing;
    public bool Revealed { get; private set; }           // opponent's strokes are visible
    public bool IsLocalDone => iDone;
    public float DrawTimeLeft => Phase == MPPhase.Drawing ? Mathf.Max(0f, (float)(drawEndAt - PhotonNetwork.Time)) : 0f;

    bool finished, started, spawned, endSent, iDone, paused;
    double drawEndAt;
    readonly HashSet<int> doneVotes = new HashSet<int>();

    public int MyIndex => Mathf.Clamp(System.Array.IndexOf(PhotonNetwork.PlayerList, PhotonNetwork.LocalPlayer), 0, spawnPoints.Length - 1);
    public Vector3 MySpawn => spawnPoints[MyIndex].position;

    readonly HashSet<int> rematchVotes = new HashSet<int>();
    bool iVoted;

    void Awake()
    {
        ChalkStroke.Multiplayer = true;
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 30;
        Instance = this;
        if (resultPanel) resultPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
        if (countdownText) countdownText.text = "";
        HideRespawn();
    }

    public override void OnDisable()
    {
        ChalkStroke.Multiplayer = false;
        base.OnDisable();
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (PhotonNetwork.InRoom) { Spawn(); return; }
        // pressed Play directly on the map: run solo for testing
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.CreateRoom("offline-test");
    }

    public override void OnJoinedRoom() => Spawn();

    void SetPhase(MPPhase p)
    {
        Phase = p;
        PhaseChanged?.Invoke(p);
    }

    void Spawn()
    {
        if (spawned) return;
        spawned = true;

        if (MPBoard.Instance)
        {
            MPBoard.Instance.Configure(MyIndex);
            MPBoard.Instance.SetDrawingView(true);
        }

        var go = PhotonNetwork.Instantiate(playerPrefabName, MySpawn, Quaternion.identity, 0,
                                           new object[] { PlayerProfile.CharacterIndex });

        var ui = FindObjectOfType<ChalkHealthUI>();
        if (ui) ui.player = go.GetComponent<ChalkPlayer>();
        onLocalPlayerSpawned?.Invoke(go.transform);

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "ready", true } });
        CheckAllReady();
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changed) => CheckAllReady();

    void CheckAllReady()
    {
        if (!PhotonNetwork.IsMasterClient || started) return;
        int expected = PhotonNetwork.OfflineMode ? 1 : 2;
        if (PhotonNetwork.PlayerList.Length < expected) return;
        foreach (var p in PhotonNetwork.PlayerList)
            if (!p.CustomProperties.TryGetValue("ready", out var r) || !(bool)r) return;

        started = true;
        photonView.RPC(nameof(RpcBeginDrawing), RpcTarget.All, PhotonNetwork.Time + drawSeconds);
    }

    // ---------- drawing phase ----------
    [PunRPC]
    void RpcBeginDrawing(double endAt)
    {
        drawEndAt = endAt;
        Revealed = false;
        iDone = false;
        endSent = false;
        doneVotes.Clear();
        if (countdownText) countdownText.text = "";
        if (MPBoard.Instance) MPBoard.Instance.SetDrawingView(true);
        DrawChalkLeft = drawChalkLimit;
        SyncChalkBar();
        SetPhase(MPPhase.Drawing);
    }

    // Done button (toggle: press again to take it back)
    public void RequestDone()
    {
        if (Phase != MPPhase.Drawing || !UsesButton) return;
        AudioManager.UIClick();
        iDone = !iDone;
        photonView.RPC(nameof(RpcDone), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, iDone);
    }

    [PunRPC]
    void RpcDone(int actor, bool done)
    {
        if (done) doneVotes.Add(actor); else doneVotes.Remove(actor);
    }

    // ---------- local pause ----------
    public void Pause()
    {
        if (paused || Phase == MPPhase.Finished) return;
        AudioManager.UIClick();
        SetPaused(true);
    }

    public void Resume()
    {
        if (!paused) return;
        AudioManager.UIClick();
        SetPaused(false);
    }

    void SetPaused(bool on)
    {
        paused = on;
        if (pausePanel) pausePanel.SetActive(on);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (paused) Resume(); else Pause();
        }
        if (Input.GetKeyDown(resetKey)) ResetMyPlayer();

        // master decides when drawing ends: timer ran out, or everyone pressed Done
        if (Phase != MPPhase.Drawing || endSent || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;
        bool timeUp = UsesTimer && PhotonNetwork.Time >= drawEndAt;
        bool allDone = UsesButton && doneVotes.Count >= PhotonNetwork.CurrentRoom.PlayerCount;
        if (timeUp || allDone)
        {
            endSent = true;
            photonView.RPC(nameof(RpcEndDrawing), RpcTarget.All, PhotonNetwork.Time + flushDelay);
        }
    }

    [PunRPC]
    void RpcEndDrawing(double revealAt)
    {
        if (Phase != MPPhase.Drawing) return;
        SetPhase(MPPhase.Reveal);                 // drawing is locked from here (DrawAllowed = false)
        StartCoroutine(RevealThenRace(revealAt));
    }

    IEnumerator RevealThenRace(double revealAt)
    {
        while (PhotonNetwork.Time < revealAt) yield return null;

        Revealed = true;
        if (MPBoard.Instance) MPBoard.Instance.SetDrawingView(false);
        foreach (var s in FindObjectsOfType<ChalkStroke>()) s.SetVisible(true);

        double goAt = revealAt + countdownSeconds;
        while (PhotonNetwork.Time < goAt)
        {
            if (countdownText) countdownText.text = Mathf.CeilToInt((float)(goAt - PhotonNetwork.Time)).ToString();
            yield return null;
        }

        SetPhase(MPPhase.Racing);
        if (NetworkPlayer.Local) NetworkPlayer.Local.SetChalk(1f);      // full chalk for the race
        eraseReadyAt = 0f;
        foreach (var s in FindObjectsOfType<ChalkStroke>()) s.SetFrozen(false);   // dynamic strokes start moving
        if (countdownText) countdownText.text = "GO!";
        yield return new WaitForSeconds(0.8f);
        if (countdownText) countdownText.text = "";
    }

    // ---- finish ----
    public void ReachedGoal()
    {
        if (Phase != MPPhase.Racing || finished) return;
        photonView.RPC(nameof(RpcGoal), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    void RpcGoal(int actor)                       // runs on the master only: first arrival wins
    {
        if (finished) return;
        finished = true;
        photonView.RPC(nameof(RpcResult), RpcTarget.All, actor);
    }

    [PunRPC]
    void RpcResult(int winnerActor)
    {
        finished = true;
        SetPhase(MPPhase.Finished);
        ShowResult(winnerActor == PhotonNetwork.LocalPlayer.ActorNumber ? "YOU WIN!" : "YOU LOSE");
    }

    public override void OnPlayerLeftRoom(Player other)
    {
        if (rematchButton) rematchButton.gameObject.SetActive(false);   // no one to rematch with
        if (finished)
        {
            if (rematchText) rematchText.text = "Opponent left";
            return;
        }
        finished = true;
        StopAllCoroutines();
        SetPhase(MPPhase.Finished);
        if (countdownText) countdownText.text = "";
        ShowResult("Opponent left - you win!");
        if (rematchButton) rematchButton.gameObject.SetActive(false);
    }

    void ShowResult(string msg)
    {
        SetPaused(false);
        HideRespawn();
        if (MPBoard.Instance) MPBoard.Instance.SetDrawingView(false);
        if (resultText) resultText.text = msg;
        if (resultPanel) resultPanel.SetActive(true);
        if (rematchButton) { rematchButton.gameObject.SetActive(true); rematchButton.interactable = true; }
        if (rematchText) rematchText.text = "";
    }

    // button hook
    public void LeaveToLobby() { AudioManager.UIClick(); PhotonNetwork.LeaveRoom(); }

    public override void OnLeftRoom()
    {
        PhotonNetwork.OfflineMode = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "ready", null } });
        SceneManager.LoadScene(lobbySceneName);
    }

    // Rematch button OnClick
    public void RequestRematch()
    {
        if (!finished || iVoted) return;
        AudioManager.UIClick();
        iVoted = true;
        if (rematchButton) rematchButton.interactable = false;
        photonView.RPC(nameof(RpcRematchVote), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    void RpcRematchVote(int actor)
    {
        rematchVotes.Add(actor);
        if (rematchText)
            rematchText.text = actor == PhotonNetwork.LocalPlayer.ActorNumber ? "Waiting for opponent..." : "Opponent wants a rematch";

        if (PhotonNetwork.IsMasterClient && rematchVotes.Count >= PhotonNetwork.CurrentRoom.PlayerCount)
            photonView.RPC(nameof(RpcRematchGo), RpcTarget.All);
    }

    [PunRPC]
    void RpcRematchGo()
    {
        HideRespawn();
        StopAllCoroutines();
        rematchVotes.Clear();
        iVoted = false;
        finished = false;

        if (resultPanel) resultPanel.SetActive(false);
        if (rematchText) rematchText.text = "";
        if (countdownText) { countdownText.gameObject.SetActive(true); countdownText.text = ""; }

        // wipe the old strokes on this client (each client holds its own copies)
        foreach (var s in FindObjectsOfType<ChalkStroke>()) Destroy(s.gameObject);
        foreach (var np in FindObjectsOfType<NetworkPlayer>()) np.ClearStrokeState();

        // put my player back at the start with full chalk
        if (NetworkPlayer.Local) NetworkPlayer.Local.ResetForRematch(MySpawn);

        // master starts a fresh drawing phase (RPCs are ordered, so this arrives after the reset)
        if (PhotonNetwork.IsMasterClient)
            photonView.RPC(nameof(RpcBeginDrawing), RpcTarget.All, PhotonNetwork.Time + drawSeconds);
    }

    public void ShowRespawn(float remaining, string reason)
    {
        if (finished) { HideRespawn(); return; }
        if (respawnPanel && !respawnPanel.activeSelf) respawnPanel.SetActive(true);
        if (respawnText) respawnText.text = reason + "\nRespawning in " + Mathf.CeilToInt(remaining);
    }

    public void HideRespawn() { if (respawnPanel) respawnPanel.SetActive(false); }
}