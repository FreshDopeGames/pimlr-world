using System.Collections.Generic;
using JUTPS;
using UnityEngine;

/// <summary>
/// PIMLR: keeps a swarm from deleting the player in under a second. Every hit on the player passes through here
/// (hooked at the top of JUHealth.DoDamage), and two rules apply:
///  1. Grace window - after a hit lands, further hits within GraceSeconds are ignored. Four grandmas swinging at
///     once count as one hit, not four.
///  2. Damage budget - the player can lose at most MaxDamagePerWindow health in any WindowSeconds.
/// With the defaults (0.35 s, 40 per 1 s) and 100 health, dying takes at least ~2.5 seconds of sustained contact,
/// enough time to dodge, roll or shoot your way out. Enemies, cars and bosses are not affected.
/// Tune the numbers here; set Enabled = false to compare against stock JUTPS damage.
/// </summary>
public static class PimlrPlayerDamageGuard
{
    public static bool Enabled = true;
    public static float GraceSeconds = 0.35f;
    public static float MaxDamagePerWindow = 40f;
    public static float WindowSeconds = 1f;

    struct Hit { public float time; public float amount; }

    static readonly List<Hit> recent = new List<Hit>();
    static JUHealth owner;
    static float lastHitTime = -999f;

    public static bool IsPlayer(JUHealth health) => health != null && health.CompareTag("Player");

    /// <summary>Returns the damage the player should actually take (0 = ignore this hit).</summary>
    public static float Filter(JUHealth health, float damage)
    {
        if (!Enabled || damage <= 0f || !IsPlayer(health) || health.IsDead) return damage;

        // New scene or new player object: start fresh.
        if (owner != health) { owner = health; recent.Clear(); lastHitTime = -999f; }

        float now = Time.time;
        if (now - lastHitTime < GraceSeconds) return 0f;

        float taken = 0f;
        for (int i = recent.Count - 1; i >= 0; i--)
        {
            if (now - recent[i].time > WindowSeconds) recent.RemoveAt(i);
            else taken += recent[i].amount;
        }

        float allowed = Mathf.Min(damage, Mathf.Max(0f, MaxDamagePerWindow - taken));
        if (allowed <= 0f) return 0f;

        recent.Add(new Hit { time = now, amount = allowed });
        lastHitTime = now;
        return allowed;
    }
}
