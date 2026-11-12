// PIMLR #GREN: water balloon pickup, the same idea as JUTPS's AmmoBox / HealthPowerUp. Walk into it to take
// balloons (up to the carry limit; a full player leaves it where it is). Place the prefab
// Assets/_Game/Prefabs/Pickups/WaterBalloonPickup.prefab wherever you want balloons in the level.
//
// Unreal note: an Actor with a sphere overlap; OnComponentBeginOverlap -> the player's grenade component Add().

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PimlrGrenadePickup : MonoBehaviour
{
    [Tooltip("Balloons given per pickup.")]
    [Min(1)] public int amount = 2;
    [Tooltip("Spawned where the pickup was taken (e.g. the JUTPS AmmoPickupCollision effect).")]
    public GameObject effect;
    [Tooltip("Comes back after this many seconds. 0 = one-time pickup.")]
    [Min(0f)] public float respawnSeconds = 45f;

    Collider trigger;
    Renderer[] renderers;
    ParticleSystem[] particles;
    bool available = true;

    void Awake()
    {
        trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>(true);
        particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!available || !other.CompareTag("Player")) return;
        var g = other.GetComponentInParent<PimlrPlayerGrenades>();
        if (!g || g.IsFull) return;               // nothing to take: leave it for later
        if (g.Add(amount) <= 0) return;

        if (effect) Destroy(Instantiate(effect, transform.position, transform.rotation), 5f);
        if (respawnSeconds <= 0f) { Destroy(gameObject); return; }
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        SetAvailable(false);
        yield return new WaitForSeconds(respawnSeconds);
        SetAvailable(true);
    }

    void SetAvailable(bool on)
    {
        available = on;
        trigger.enabled = on;
        foreach (var r in renderers) if (r) r.enabled = on;
        foreach (var p in particles) { if (!p) continue; if (on) p.Play(true); else p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    }
}
