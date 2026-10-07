using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Needs a PhotonView on the same object (no observed components).
public class MPMatchManager : MonoBehaviourPunCallbacks
{
    public static MPMatchManager Instance { get; private set; }
    public static bool CanPlay => Instance == null || Instance.running;

    public string playerPrefabName = "MPPlayer";        // prefab inside a Resources folder
    public Transform[] spawnPoints;                      // [0] = first player, [1] = second player
    public string lobbySceneName = "MP_Lobby";
    public int countdownSeconds = 3;

    [Header("UI")]
    public TMP_Text countdownText;
    public GameObject resultPanel;
    public TMP_Text resultText;

    [Header("Hook your camera follow here (Transform = local player)")]
    public UnityEvent<Transform> onLocalPlayerSpawned;

    bool running, finished, started, spawned;

    int MyIndex => Mathf.Clamp(System.Array.IndexOf(PhotonNetwork.PlayerList, PhotonNetwork.LocalPlayer), 0, spawnPoints.Length - 1);
    public Vector3 MySpawn => spawnPoints[MyIndex].position;

    void Awake()
    {
        Instance = this;
        if (resultPanel) resultPanel.SetActive(false);
        if (countdownText) countdownText.text = "";
    }

    public override void OnDisable() { base.OnDisable(); if (Instance == this) Instance = null; }

    void Start()
    {
        if (PhotonNetwork.InRoom) { Spawn(); return; }
        // pressed Play directly on the map: run solo for testing
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.CreateRoom("offline-test");
    }

    public override void OnJoinedRoom() => Spawn();

    void Spawn()
    {
        if (spawned) return;
        spawned = true;

        var go = PhotonNetwork.Instantiate(playerPrefabName, MySpawn, Quaternion.identity, 0,
                                           new object[] { PlayerProfile.CharacterIndex });

        var ui = FindObjectOfType<ChalkHealthUI>();
        if (ui) ui.player = go.GetComponent<ChalkPlayer>();
        onLocalPlayerSpawned?.Invoke(go.transform);

        // "I'm loaded" (tied to the room name so old values from a previous match are ignored)
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "ready", PhotonNetwork.CurrentRoom.Name } });
        CheckAllReady();
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changed) => CheckAllReady();

    void CheckAllReady()
    {
        if (!PhotonNetwork.IsMasterClient || started) return;
        int expected = PhotonNetwork.OfflineMode ? 1 : 2;
        if (PhotonNetwork.PlayerList.Length < expected) return;
        foreach (var p in PhotonNetwork.PlayerList)
            if (!p.CustomProperties.TryGetValue("ready", out var r) || (string)r != PhotonNetwork.CurrentRoom.Name) return;

        started = true;
        photonView.RPC(nameof(RpcStartCountdown), RpcTarget.All, PhotonNetwork.Time + countdownSeconds);
    }

    [PunRPC]
    void RpcStartCountdown(double startAt) => StartCoroutine(Countdown(startAt));

    IEnumerator Countdown(double startAt)
    {
        while (PhotonNetwork.Time < startAt)
        {
            if (countdownText) countdownText.text = Mathf.CeilToInt((float)(startAt - PhotonNetwork.Time)).ToString();
            yield return null;
        }
        running = true;
        if (countdownText) countdownText.text = "GO!";
        yield return new WaitForSeconds(0.8f);
        if (countdownText) countdownText.text = "";
    }

    // ---- finish ----
    public void ReachedGoal()
    {
        if (!running || finished) return;
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
        running = false;
        ShowResult(winnerActor == PhotonNetwork.LocalPlayer.ActorNumber ? "YOU WIN!" : "YOU LOSE");
    }

    public override void OnPlayerLeftRoom(Player other)
    {
        if (finished) return;
        finished = true; running = false;
        ShowResult("Opponent left - you win!");
    }

    void ShowResult(string msg)
    {
        if (resultText) resultText.text = msg;
        if (resultPanel) resultPanel.SetActive(true);
    }

    // button hook
    public void LeaveToLobby() { AudioManager.UIClick(); PhotonNetwork.LeaveRoom(); }

    public override void OnLeftRoom()
    {
        PhotonNetwork.OfflineMode = false;
        SceneManager.LoadScene(lobbySceneName);
    }
}