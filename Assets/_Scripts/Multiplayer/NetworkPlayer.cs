using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(PhotonView))]
public class NetworkPlayer : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback
{
    public static NetworkPlayer Local { get; private set; }

    public float respawnDelay = 1.5f;
    public float liveSendInterval = 0.05f;      // 20 updates/sec; lower = smoother, more traffic

    [Header("Name tag")]
    public TMP_FontAsset nameFont;                 // optional, e.g. your Chalk SDF
    public float nameSize = 3f;
    public float nameHeight = 0.35f;               // gap above the head
    public Color localNameColor = new Color(0.6f, 1f, 0.6f);
    public Color opponentNameColor = Color.white;

    TextMeshPro nameTag;
    Collider2D bodyCol;

    ChalkPlayer chalk;
    ChalkDrawer drawer;
    ChalkPlayerController controller;
    ChalkPlayerFX fx;
    Rigidbody2D rb;

    // owner side: points waiting to be sent
    readonly List<float> pending = new List<float>();
    float nextSend;

    // remote side: the opponent's line while they draw
    LineRenderer live;
    int livePointCount;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        chalk = GetComponent<ChalkPlayer>();
        controller = GetComponent<ChalkPlayerController>();
        fx = GetComponent<ChalkPlayerFX>();
        rb = GetComponent<Rigidbody2D>();

        drawer = GetComponentInChildren<ChalkDrawer>();
        if (!drawer) drawer = FindObjectOfType<ChalkDrawer>();
        bool drawerOnPlayer = drawer && drawer.transform.IsChildOf(transform);

        object[] data = photonView.InstantiationData;
        int character = (data != null && data.Length > 0) ? (int)data[0] : 0;
        var applier = GetComponent<CharacterApplier>();
        if (applier) applier.Apply(character);
        var body = GetComponent<ChalkBodyHealth>();
        if (body) body.Capture();

        if (photonView.IsMine)
        {
            Local = this;
            chalk.regenEnabled = true;
            chalk.Died += OnDied;
            if (drawer)
            {
                drawer.player = chalk;
                drawer.StrokeCreated += SendStroke;
                drawer.StrokeBegan += OnStrokeBegan;
                drawer.PointAdded += OnPointAdded;
                drawer.StrokeEnded += OnStrokeEnded;
            }
            else Debug.LogError("NetworkPlayer: no ChalkDrawer found", this);
        }
        else
        {
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
        if (nameTag) Destroy(nameTag.gameObject);
        if (live) Destroy(live.gameObject);
        if (photonView && photonView.IsMine && drawer)
        {
            drawer.StrokeCreated -= SendStroke;
            drawer.StrokeBegan -= OnStrokeBegan;
            drawer.PointAdded -= OnPointAdded;
            drawer.StrokeEnded -= OnStrokeEnded;
        }
    }

    // ---------- respawn ----------
    void OnDied() { StartCoroutine(Respawn()); }

    IEnumerator Respawn()
    {
        controller.enabled = false;
        rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(respawnDelay);

        Vector3 p = MPMatchManager.Instance ? MPMatchManager.Instance.MySpawn : transform.position;
        rb.position = p;
        transform.position = p;
        rb.velocity = Vector2.zero;
        chalk.Revive();
        controller.enabled = true;
    }

    // ---------- finished strokes (reliable, builds the real solid stroke) ----------
    void SendStroke(Vector2[] pts, float life, bool dyn, Color c)
    {
        var f = new float[pts.Length * 2];
        for (int i = 0; i < pts.Length; i++) { f[i * 2] = pts[i].x; f[i * 2 + 1] = pts[i].y; }
        photonView.RPC(nameof(RpcStroke), RpcTarget.Others, f, life, dyn, c.r, c.g, c.b);
    }

    [PunRPC]
    void RpcStroke(float[] f, float life, bool dyn, float r, float g, float b)
    {
        var pts = new Vector2[f.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(f[i * 2], f[i * 2 + 1]);
        drawer.SpawnStroke(pts, life, dyn, new Color(r, g, b, 1f));
    }

    // ---------- live drawing: owner side ----------
    void OnStrokeBegan(Color c)
    {
        pending.Clear();
        photonView.RPC(nameof(RpcLiveBegin), RpcTarget.Others, c.r, c.g, c.b);
    }

    void OnPointAdded(Vector2 p) { pending.Add(p.x); pending.Add(p.y); }

    void OnStrokeEnded()
    {
        pending.Clear();
        photonView.RPC(nameof(RpcLiveEnd), RpcTarget.Others);   // arrives after the final stroke, so no flicker
    }

    void Update()
    {
        if (!photonView.IsMine || pending.Count == 0 || Time.unscaledTime < nextSend) return;
        photonView.RPC(nameof(RpcLivePoints), RpcTarget.Others, pending.ToArray());
        pending.Clear();
        nextSend = Time.unscaledTime + liveSendInterval;
    }

    // ---------- live drawing: opponent's screen ----------
    LineRenderer CreateLive()
    {
        float w = drawer ? drawer.lineWidth : 0.15f;
        var lr = new GameObject("OpponentLiveLine").AddComponent<LineRenderer>();
        ChalkVisuals.Apply(lr, w);
        lr.startWidth = lr.endWidth = w;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.sortingOrder = 10;
        lr.positionCount = 0;
        return lr;
    }

    [PunRPC]
    void RpcLiveBegin(float r, float g, float b)
    {
        if (!live) live = CreateLive();
        livePointCount = 0;
        live.positionCount = 0;
        live.startColor = live.endColor = new Color(r, g, b, 1f);
    }

    [PunRPC]
    void RpcLivePoints(float[] f)
    {
        if (!live) return;
        int add = f.Length / 2;
        live.positionCount = livePointCount + add;
        for (int i = 0; i < add; i++)
            live.SetPosition(livePointCount + i, new Vector3(f[i * 2], f[i * 2 + 1], 0f));
        livePointCount += add;
    }

    [PunRPC]
    void RpcLiveEnd()
    {
        livePointCount = 0;
        if (live) live.positionCount = 0;
    }

    // ---------- health sync (position/scale: PhotonTransformView) ----------
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) stream.SendNext(chalk.health);
        else chalk.health = (float)stream.ReceiveNext();
    }

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
        nameTag.sortingOrder = 20;                 // above strokes and the player
    }

    void LateUpdate()
    {
        if (!nameTag) return;
        float top = bodyCol ? bodyCol.bounds.max.y : transform.position.y + 1f;
        nameTag.transform.position = new Vector3(transform.position.x, top + nameHeight, 0f);
    }
}