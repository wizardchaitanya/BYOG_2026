using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.UI;

public class MPLobby : MonoBehaviourPunCallbacks
{
    [Header("Rooms")]
    public int roomCount = 5;                         // 2 players each
    public string[] mapScenes = { "MP_Map1" };        // one is picked at random when a room fills
    public string menuSceneName = "MainMenu";

    [Header("UI")]
    public MPMapButton roomButtonPrefab;              // your existing button prefab (name + status text)
    public Transform roomButtonContainer;             // Layout Group
    public GameObject cancelButton;
    public Button backButton;          // the Main Menu button; needs: using UnityEngine.UI;
    public TMP_Text statusText;

    readonly List<MPMapButton> buttons = new List<MPMapButton>();
    readonly Dictionary<string, RoomInfo> rooms = new Dictionary<string, RoomInfo>();
    int myRoom = -1;                                  // room I joined / am joining, -1 = none

    static string RoomName(int i) => "chalkroom" + (i + 1);

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        if (string.IsNullOrEmpty(PhotonNetwork.NickName))
            PhotonNetwork.NickName = PlayerProfile.PlayerName;

        for (int i = 0; i < roomCount; i++)
        {
            int idx = i;
            var b = Instantiate(roomButtonPrefab, roomButtonContainer);
            b.Setup("Room " + (i + 1), () => JoinRoom(idx));
            buttons.Add(b);
        }
        Refresh();

        if (PhotonNetwork.InLobby) OnJoinedLobby();
        else if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer) PhotonNetwork.JoinLobby();
        else if (!PhotonNetwork.IsConnected)
        {
            Status("Connecting...");
            PhotonNetwork.GameVersion = "1.0";
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    void Status(string s) { if (statusText) statusText.text = s; }

    // ---------- connection ----------
    public override void OnConnectedToMaster() { if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby(); }
    public override void OnJoinedLobby() { myRoom = -1; Status("Pick a room"); Refresh(); }
    public override void OnLeftLobby() { rooms.Clear(); }
    public override void OnDisconnected(DisconnectCause cause) { myRoom = -1; Status("Disconnected: " + cause); Refresh(); }

    // ---------- room list ----------
    public override void OnRoomListUpdate(List<RoomInfo> list)
    {
        foreach (var r in list)
        {
            if (r.RemovedFromList) rooms.Remove(r.Name);
            else rooms[r.Name] = r;
        }
        Refresh();
    }

    void Refresh()
    {
        bool ready = PhotonNetwork.InLobby;
        bool busy = myRoom >= 0;

        for (int i = 0; i < buttons.Count; i++)
        {
            rooms.TryGetValue(RoomName(i), out RoomInfo r);
            string s;
            bool canJoin;

            if (myRoom == i)                                   { s = "You are waiting..."; canJoin = false; }
            else if (r == null || r.PlayerCount == 0)          { s = "Empty"; canJoin = true; }
            else if (!r.IsOpen || r.PlayerCount >= r.MaxPlayers) { s = "Full - match in progress"; canJoin = false; }
            else
            {
                string host = null;
                if (r.CustomProperties.TryGetValue("host", out var h)) host = h as string;
                s = (host ?? "Someone") + " is waiting";
                canJoin = true;
            }

            buttons[i].SetStatus(s);
            buttons[i].SetInteractable(ready && !busy && canJoin);
        }
        if (cancelButton) cancelButton.SetActive(busy);
        if (backButton) backButton.interactable = !busy && !PhotonNetwork.InRoom;
    }

    // ---------- joining ----------
    public void JoinRoom(int i)
    {
        AudioManager.UIClick();
        myRoom = i;
        Status("Joining Room " + (i + 1) + "...");
        Refresh();

        // joins the room if it exists, creates it if it doesn't
        PhotonNetwork.JoinOrCreateRoom(RoomName(i), new RoomOptions
        {
            MaxPlayers = 2,
            CustomRoomProperties = new Hashtable { { "host", PhotonNetwork.NickName } },
            CustomRoomPropertiesForLobby = new[] { "host" }    // visible in the room list
        }, TypedLobby.Default);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        myRoom = -1;
        Status("Room not available, pick another");
        Refresh();
    }

    public override void OnCreateRoomFailed(short returnCode, string message) => OnJoinRoomFailed(returnCode, message);

    public override void OnJoinedRoom()
    {
        Status("Waiting in " + (myRoom >= 0 ? "Room " + (myRoom + 1) : "room") + " for an opponent...");
        TryStart();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => TryStart();

    void TryStart()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom.PlayerCount < 2) return;
        PhotonNetwork.CurrentRoom.IsOpen = false;              // stays listed, shows as "Full"
        Status("Opponent found!");
        PhotonNetwork.LoadLevel(mapScenes[Random.Range(0, mapScenes.Length)]);   // random map
    }

    public override void OnLeftRoom() { myRoom = -1; Refresh(); }

    public void Cancel()
    {
        AudioManager.UIClick();
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();   // back in the lobby afterwards, UI resets
    }

    public void Back()
    {
        if (myRoom >= 0 || PhotonNetwork.InRoom) return;     // must Cancel the room first
        AudioManager.UIClick();
        PhotonNetwork.AutomaticallySyncScene = false;        // stops Photon touching room props during the scene change
        PhotonNetwork.Disconnect();
        SceneManager.LoadScene(menuSceneName);
    }
}