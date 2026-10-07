using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathTrigger : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        var p = other.GetComponentInParent<ChalkPlayer>();
        if (p) p.Kill("Fell into the void");
    }
}
