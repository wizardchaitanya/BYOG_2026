using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

// On the MPPlayer prefab. PhotonView observes: PhotonTransformView + this script.
// Split-board mode: no live drawing. Finished strokes are sent to the opponent (hidden + frozen until the reveal),
// erased strokes are removed on the opponent's side, dynamic strokes are synced during the race.
[RequireComponent(typeof(PhotonView))]
public class NetworkPlayer : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback
{
    public static NetworkPlayer Local { get; private set; }

    public float respawnDelay = 1.5f;

    [Header("Name tag")]
    public TMP_FontAsset nameFont;
    public float nameSize = 3f;
    public float nameHeight = 0.35f;
    public Color localNameColor = new Color(0.6f, 1f, 0.6f);
    public Color opponentNameColor = Color.white;

    ChalkPlayer chalk;
    ChalkDrawer drawer;
    ChalkPlayerController controller;
    ChalkPlayerFX fx;
    Rigidbody2D rb;
    SpriteRenderer[] sprites;

    public ChalkDrawer Drawer => drawer;

    // strokes
    int nextStrokeId;
    readonly Dictionary<int, ChalkStroke> owned = new Dictionary<int, ChalkStroke>();         // my strokes (static + dynamic)
    readonly Dictionary<int, ChalkStroke> remoteStrokes = new Dictionary<int, ChalkStroke>(); // opponent's copies
    readonly List<int> goneBuf = new List<int>();

    // name tag
    TextMeshPro nameTag;
    Collider2D bodyCol;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        chalk = GetComponent<ChalkPlayer>();
        controller = GetComponent<ChalkPlayerController>();
        fx = GetComponent<ChalkPlayerFX>();
        rb = GetComponent<Rigidbody2D>();

        // drawer on the player prefab, or the one sitting in the scene
        drawer = GetComponentInChildren<ChalkDrawer>();
        if (!drawer) drawer = FindObjectOfType<ChalkDrawer>();
        bool drawerOnPlayer = drawer && drawer.transform.IsChildOf(transform);

        // apply the OWNER's character on every client
        object[] data = photonView.InstantiationData;
        int character = (data != null && data.Length > 0) ? (int)data[0] : 0;
        var applier = GetComponent<CharacterApplier>();
        if (applier) applier.Apply(character);
        var body = GetComponent<ChalkBodyHealth>();
        if (body) body.Capture();
        sprites = GetComponentsInChildren<SpriteRenderer>(true);

        if (photonView.IsMine)
        {
            Local = this;
            chalk.regenEnabled = true;
            chalk.noChalkDeath = true;          // running out of chalk doesn't kill, it just stops you drawing
            chalk.Died += OnDied;
            if (drawer)
            {
                drawer.player = chalk;
                drawer.StrokeCreated += SendStroke;
            }
            else Debug.LogError("NetworkPlayer: no ChalkDrawer found", this);
        }
        else
        {
            // opponent's copy: no input, no physics of its own, PhotonTransformView moves it
            chalk.isRemote = true;
            chalk.enabled = false;
            controller.enabled = false;
            if (drawerOnPlayer) drawer.enabled = false;
            if (fx) fx.enabled = false;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
        }

        CreateNameTag();
    }

    void OnDestroy()
    {
        if (Local == this) Local = null;
        if (nameTag) Destroy(nameTag.gameObject);
        if (photonView && photonView.IsMine && drawer)
            drawer.StrokeCreated -= SendStroke;
    }

    // ---------- respawn / rematch ----------
    void OnDied() { StartCoroutine(Respawn()); }

    IEnumerator Respawn()
    {
        controller.enabled = false;
        SetDeadState(true);
        photonView.RPC(nameof(RpcDead), RpcTarget.Others, true);

        float end = Time.time + respawnDelay;
        var m = MPMatchManager.Instance;
        while (Time.time < end)
        {
            if (m) m.ShowRespawn(end - Time.time, chalk.LastDeathReason);
            yield return null;
        }
        if (m) m.HideRespawn();

        Vector3 p = m ? m.MySpawn : transform.position;
        rb.position = p;
        transform.position = p;
        SetDeadState(false);
        rb.velocity = Vector2.zero;
        chalk.Revive();
        controller.enabled = true;
        photonView.RPC(nameof(RpcDead), RpcTarget.Others, false);
    }

    // dead = hidden and frozen (no falling, no collisions) until the timer ends
    void SetDeadState(bool dead)
    {
        if (sprites != null)
            foreach (var sr in sprites) if (sr) sr.enabled = !dead;
        if (nameTag) nameTag.gameObject.SetActive(!dead);
        rb.simulated = !dead;
        if (dead) rb.velocity = Vector2.zero;
    }

    [PunRPC]
    void RpcDead(bool dead)
    {
        if (photonView.IsMine) return;
        if (dead) SetDeadState(true);
        else StartCoroutine(ShowGhostLater());
    }

    // let PhotonTransformView teleport the ghost to the spawn first, so it doesn't flash at the death spot
    IEnumerator ShowGhostLater()
    {
        yield return new WaitForSeconds(0.2f);
        SetDeadState(false);
    }

    public void ClearStrokeState()
    {
        owned.Clear();
        remoteStrokes.Clear();
    }

    public void ResetForRematch(Vector3 spawn)
    {
        StopAllCoroutines();                       // cancel a pending respawn
        SetDeadState(false);
        rb.position = spawn;
        transform.position = spawn;
        rb.velocity = Vector2.zero;
        chalk.Revive();
        controller.enabled = true;
    }

    // R key / reset button: back to the start, chalk is NOT refilled
    public void ResetToSpawn()
    {
        var m = MPMatchManager.Instance;
        if (!m || !chalk || !chalk.enabled) return;     // dead / respawning already
        Vector3 p = m.MySpawn;
        rb.position = p;
        transform.position = p;
        rb.velocity = Vector2.zero;
    }

    // set chalk to a fraction of full (drawing-phase budget, or 1 = full at GO)
    public void SetChalk(float fraction)
    {
        if (chalk) chalk.health = chalk.maxHealth * Mathf.Clamp01(fraction);
    }

    // ---------- strokes ----------
    void SendStroke(Vector2[] pts, float life, bool dyn, Color c)
    {
        int id = ++nextStrokeId;
        if (drawer.LastStroke) owned[id] = drawer.LastStroke;

        var f = new float[pts.Length * 2];
        for (int i = 0; i < pts.Length; i++) { f[i * 2] = pts[i].x; f[i * 2 + 1] = pts[i].y; }
        photonView.RPC(nameof(RpcStroke), RpcTarget.Others, f, life, dyn, c.r, c.g, c.b, id, PhotonNetwork.Time);
    }

    [PunRPC]
    void RpcStroke(float[] f, float life, bool dyn, float r, float g, float b, int id, double sentAt)
    {
        var pts = new Vector2[f.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(f[i * 2], f[i * 2 + 1]);

        var s = drawer.SpawnStroke(pts, life, dyn, new Color(r, g, b, 1f));

        // drawing phase: opponent's strokes stay hidden and dynamic ones frozen until the reveal / GO
        var m = MPMatchManager.Instance;
        if (m)
        {
            s.SetFrozen(!m.Racing);
            s.SetPermanent(!m.Racing);          // drawing-phase strokes stay, race strokes dissolve like before
            if (!m.Revealed) s.SetVisible(false);
        }

        if (id > 0)
        {
            remoteStrokes[id] = s;
            if (dyn && s.Body) s.MarkReplica(Mathf.Clamp((float)(PhotonNetwork.Time - sentAt), 0f, 0.5f));
        }
    }

    [PunRPC]
    void RpcStrokeGone(int id)
    {
        if (remoteStrokes.TryGetValue(id, out var s))
        {
            remoteStrokes.Remove(id);
            if (s) Destroy(s.gameObject);
        }
    }

    // race eraser: remove a race stroke, mine or the opponent's. For the opponent's, ask the owner to delete the original.
    public static void EraseRaceStroke(ChalkStroke s)
    {
        foreach (var np in FindObjectsOfType<NetworkPlayer>())
        {
            foreach (var kv in np.owned)
                if (kv.Value == s)
                {
                    if (np.photonView.IsMine) Destroy(s.gameObject);     // PruneOwned tells the opponent
                    return;
                }

            foreach (var kv in np.remoteStrokes)
                if (kv.Value == s)
                {
                    int id = kv.Key;
                    np.remoteStrokes.Remove(id);
                    np.photonView.RPC(nameof(RpcEraseRequest), RpcTarget.Others, id);
                    Destroy(s.gameObject);
                    return;
                }
        }
    }

    [PunRPC]
    void RpcEraseRequest(int id)
    {
        if (!photonView.IsMine) return;
        if (owned.TryGetValue(id, out var s) && s && !s.IsPermanent) Destroy(s.gameObject);
    }

    // my stroke was erased / crushed -> tell the opponent to remove their copy
    void PruneOwned()
    {
        if (owned.Count == 0) return;
        goneBuf.Clear();
        foreach (var kv in owned) if (!kv.Value) goneBuf.Add(kv.Key);
        foreach (int id in goneBuf)
        {
            owned.Remove(id);
            photonView.RPC(nameof(RpcStrokeGone), RpcTarget.Others, id);
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;
        PruneOwned();
    }

    // ---------- name tag ----------
    void CreateNameTag()
    {
        foreach (var c in GetComponentsInChildren<Collider2D>())
            if (!c.isTrigger) { bodyCol = c; break; }

        var go = new GameObject("NameTag");        // not parented, so the player's mirroring can't flip it
        nameTag = go.AddComponent<TextMeshPro>();
        if (nameFont) nameTag.font = nameFont;

        string n = photonView.Owner != null ? photonView.Owner.NickName : null;
        nameTag.text = string.IsNullOrEmpty(n) ? "Player" : n;
        nameTag.fontSize = nameSize;
        nameTag.alignment = TextAlignmentOptions.Center;
        nameTag.enableWordWrapping = false;
        nameTag.color = photonView.IsMine ? localNameColor : opponentNameColor;
        nameTag.outlineWidth = 0.2f;
        nameTag.outlineColor = new Color32(0, 0, 0, 255);
        nameTag.rectTransform.sizeDelta = new Vector2(8f, 1f);
        nameTag.sortingOrder = 20;
    }

    void LateUpdate()
    {
        if (!nameTag) return;
        float top = bodyCol ? bodyCol.bounds.max.y : transform.position.y + 1f;
        nameTag.transform.position = new Vector3(transform.position.x, top + nameHeight, 0f);
    }

    // ---------- sync (position/scale: PhotonTransformView) ----------
    static bool Dyn(ChalkStroke s) { return s && s.Body; }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(chalk.health);

            // dynamic strokes are only synced during the race (before that they are frozen)
            var m = MPMatchManager.Instance;
            bool send = m && m.Racing;
            int n = 0;
            if (send) foreach (var kv in owned) if (Dyn(kv.Value)) n++;
            stream.SendNext(n);
            if (!send) return;

            foreach (var kv in owned)
            {
                var s = kv.Value;
                if (!Dyn(s)) continue;
                stream.SendNext(kv.Key);
                stream.SendNext((Vector2)s.transform.position);
                stream.SendNext(s.transform.eulerAngles.z);
                stream.SendNext(s.Age01);
                stream.SendNext(s.Body.velocity);
                stream.SendNext(s.Body.angularVelocity);
            }
        }
        else
        {
            chalk.health = (float)stream.ReceiveNext();
            float lag = Mathf.Clamp((float)(PhotonNetwork.Time - info.SentServerTime), 0f, 0.5f);
            int n = (int)stream.ReceiveNext();
            for (int i = 0; i < n; i++)
            {
                int id = (int)stream.ReceiveNext();
                Vector2 p = (Vector2)stream.ReceiveNext();
                float rot = (float)stream.ReceiveNext();
                float age = (float)stream.ReceiveNext();
                Vector2 vel = (Vector2)stream.ReceiveNext();
                float angVel = (float)stream.ReceiveNext();
                if (remoteStrokes.TryGetValue(id, out var s) && s) s.SetNetState(p, rot, age, vel, angVel, lag);
            }
        }
    }
}