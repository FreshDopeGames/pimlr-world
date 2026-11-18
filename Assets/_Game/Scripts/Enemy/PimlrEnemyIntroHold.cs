// PIMLR #CAM (Nov 16): the Story "first enemy" cutscene is an introduction - the camera flies to the portal so the
// player gets a good look at the robot. The enemy used to step out with its AI live, spot the player and charge
// while the camera was still on it. Now it steps out and stands in place, facing the player, until the cinematic
// camera has handed back to the gameplay camera (MissionCameraDirector.IsPlaying goes false); then every AI driver
// it had is switched back on.
//
// Same technique as FanAggroBrain.SetHeld (Save The Fans intros): brain components off + zero movement input each frame.
// Added by PimlrEnemySpawnDirector.ArriveRoutine, after the Gunner / Hunt / Seek drivers are attached, so those are
// held too. If the enemy dies while held (e.g. a stray splash), it is simply released without restoring anything.
//
// Unreal note: the AIController's BrainComponent->PauseLogic("IntroCutscene") on spawn and ResumeLogic when the
// Level Sequence's OnFinished fires (or when SetViewTargetWithBlend back to the player completes); StopMovement().

using System;
using System.Collections.Generic;
using JUTPS;
using JUTPS.AI;
using UnityEngine;

[DisallowMultipleComponent]
public class PimlrEnemyIntroHold : MonoBehaviour
{
    // MonoBehaviours that drive an enemy on their own (same list as PimlrEnemyAnimTester), plus the JUTPS AI brain.
    static readonly string[] DriverTypeNames =
    {
        "GrenadeThrower", "PimlrBomberAI", "PimlrGunnerAI", "PimlrHuntPlayer", "AIRandomDestination",
        "EnemyCombatAI", "EnemyNavmeshLeash", "PimlrSeekIntersection",
    };

    /// <summary>Safety net: never hold an enemy longer than this, even if the camera never reports back.</summary>
    public static float MaxHoldSeconds = 20f;

    readonly List<Behaviour> heldOff = new List<Behaviour>();
    Func<bool> holdWhile;
    JUCharacterController ctrl;
    JUHealth health;
    Quaternion facing;
    float releaseAt;

    public static bool IsHeld(GameObject enemy) => enemy && enemy.TryGetComponent(out PimlrEnemyIntroHold h) && h.enabled;

    /// <summary>Freeze this enemy's AI while holdWhile() is true (checked every frame).</summary>
    public static void Hold(GameObject enemy, Func<bool> holdWhile)
    {
        if (!enemy || holdWhile == null) return;
        if (!enemy.TryGetComponent(out PimlrEnemyIntroHold h)) h = enemy.AddComponent<PimlrEnemyIntroHold>();
        h.Begin(holdWhile);
    }

    void Begin(Func<bool> until)
    {
        holdWhile = until;
        ctrl = GetComponent<JUCharacterController>();
        health = GetComponent<JUHealth>();
        facing = transform.rotation;
        releaseAt = Time.time + MaxHoldSeconds;
        foreach (var b in GetComponents<Behaviour>())
        {
            if (!b || b == this || !b.enabled) continue;
            if (b is JUCharacterArtificialInteligenceBrain || Array.IndexOf(DriverTypeNames, b.GetType().Name) >= 0)
            {
                b.enabled = false;
                if (!heldOff.Contains(b)) heldOff.Add(b);
            }
        }
        StandStill();
    }

    void Update()
    {
        if (health && health.IsDead) { heldOff.Clear(); Destroy(this); return; }
        bool keep = Time.time < releaseAt;
        if (keep) { try { keep = holdWhile(); } catch { keep = false; } }
        if (!keep) { Release(); return; }

        // Something (e.g. the Freeze Ray ending) may have switched a driver back on: keep them off while held.
        foreach (var b in heldOff) if (b && b.enabled) b.enabled = false;
        StandStill();
    }

    void StandStill()
    {
        if (!ctrl) return;
        ctrl._Move(0f, 0f, false);
        ctrl.FiringMode = false;
        ctrl.FiringModeIK = false;
        ctrl.IsAiming = false;
        transform.rotation = facing;   // keep facing the player, as it stepped out of the portal
    }

    void Release()
    {
        foreach (var b in heldOff) if (b) b.enabled = true;
        heldOff.Clear();
        Destroy(this);
    }
}
