using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class MPLobby : MonoBehaviourPunCallbacks
{
    public string[] mapScenes = { "MP_Map1" };      // must be in Build Settings
    public string menuSceneName = "MainMenu";
    public Button[] mapButtons;                      // one per map; OnClick -> FindMatch(index)
    public GameObject cancelButton;
    public TMP_Text statusText;

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;   // master loads the map for both
        PhotonNetwork.NickName = "Player" + Random.Range(1000, 9999);
        SetButtons(false);
        if (cancelButton) cancelButton.SetActive(false);

        if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer) Ready();
        else if (!PhotonNetwork.IsConnected)
        {
            Status("Connecting...");
            PhotonNetwork.GameVersion = "1.0";
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    void Status(string s) { if (statusText) statusText.text = s; }
    void SetButtons(bool on) { foreach (var b in mapButtons) if (b) b.interactable = on; }
    void Ready() { Status("Choose a map"); SetButtons(true); if (cancelButton) cancelButton.SetActive(false); }

    public override void OnConnectedToMaster() => Ready();
    public override void OnDisconnected(DisconnectCause cause) { Status("Disconnected: " + cause); SetButtons(false); }

    public void FindMatch(int mapIndex)
    {
        AudioManager.UIClick();
        SetButtons(false);
        if (cancelButton) cancelButton.SetActive(true);
        Status("Searching for opponent...");

        var props = new Hashtable { { "map", mapScenes[mapIndex] } };
        PhotonNetwork.JoinRandomOrCreateRoom(
            expectedCustomRoomProperties: props,
            expectedMaxPlayers: 2,
            roomOptions: new RoomOptions
            {
                MaxPlayers = 2,
                CustomRoomProperties = props,
                CustomRoomPropertiesForLobby = new[] { "map" }
            });
    }

    public void Cancel() { AudioManager.UIClick(); if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(); }

    public void Back()
    {
        AudioManager.UIClick();
        PhotonNetwork.Disconnect();
        SceneManager.LoadScene(menuSceneName);
    }

    public override void OnJoinedRoom() { Status("Waiting for opponent (1/2)..."); TryStart(); }
    public override void OnPlayerEnteredRoom(Player newPlayer) => TryStart();

    void TryStart()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom.PlayerCount < 2) return;
        PhotonNetwork.CurrentRoom.IsOpen = false;
        Status("Opponent found!");
        PhotonNetwork.LoadLevel((string)PhotonNetwork.CurrentRoom.CustomProperties["map"]);
    }
}