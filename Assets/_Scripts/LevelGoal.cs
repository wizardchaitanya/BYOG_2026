using UnityEngine;

// Finish line. Put on an object with a trigger Collider2D at the end of the level.
[RequireComponent(typeof(Collider2D))]
public class LevelGoal : MonoBehaviour
{
    void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<ChalkPlayer>();
        if (!player) return;
        if (MPMatchManager.Instance) { if (!player.isRemote) MPMatchManager.Instance.ReachedGoal(); return; }
        if (GameManager.Instance) GameManager.Instance.CompleteLevel();
    }
}
