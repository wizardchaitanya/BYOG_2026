using UnityEngine;

// Trigger zone. Fires when the player walks into it.
[RequireComponent(typeof(Collider2D))]
public class TutorialZone : MonoBehaviour
{
    public event System.Action Entered;
    void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<ChalkPlayer>()) Entered?.Invoke();
    }
}