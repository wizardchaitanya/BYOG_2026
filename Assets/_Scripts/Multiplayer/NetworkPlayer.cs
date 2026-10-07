using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class NetworkPlayer : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback
{
    public static NetworkPlayer Local { get; private set; }
    public float respawnDelay = 1.5f;

    ChalkPlayer chalk;
    ChalkDrawer drawer;
    ChalkPlayerController controller;
    ChalkPlayerFX fx;
    Rigidbody2D rb;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        chalk = GetComponent<ChalkPlayer>();
        drawer = GetComponent<ChalkDrawer>();
        controller = GetComponent<ChalkPlayerController>();
        fx = GetComponent<ChalkPlayerFX>();
        rb = GetComponent<Rigidbody2D>();

        // apply the OWNER's character on every client
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
            chalk.Died += () => StartCoroutine(Respawn());
            drawer.StrokeCreated += SendStroke;
        }
        else
        {
            // opponent's copy: no input, no physics, just follows the owner
            chalk.isRemote = true;
            chalk.enabled = false;
            controller.enabled = false;
            drawer.enabled = false;
            if (fx) fx.enabled = false;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
        }
    }

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

    // ---- strokes ----
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

    // ---- health sync (position/scale handled by PhotonTransformView) ----
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) stream.SendNext(chalk.health);
        else chalk.health = (float)stream.ReceiveNext();
    }
}