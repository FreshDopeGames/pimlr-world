// PIMLR #STF (Save The Fans) — added at spawn time to every mission enemy.
// Decides who the enemy is after (fan or player) and steers the EXISTING JUTPS AI to do it,
// the same way PimlrHuntPlayer / PimlrSeekIntersection already do:
//
//   Going for the fan   -> the brain's TargetTags are emptied, so ZombieAI / PatrolAI see no
//                          target and walk to brain.Destination, which this script keeps on a
//                          spot beside the fan. PimlrGunnerAI / PimlrBomberAI are switched off
//                          so they don't override the movement.
//   Grudge on the player-> TargetTags are restored, senses are widened (as PimlrHuntPlayer does)
//                          and Destination follows the player. The enemy's own AI then sees the
//                          player and attacks with its normal behaviour.
//
// Runs at execution order 60: after PatrolAI/ZombieAI (0) and PimlrGunnerAI/PimlrBomberAI (50).
// That matters for PatrolAI, which overwrites Destination inside its own Update — the last
// writer in the frame is the one its once-per-0.35s path recalculation uses.
//
// The fan is washed by proximity (FanHeat's wash meter), not by JUTPS damage.
//
// Unreal note: becomes the enemy's AAIController + Behavior Tree. Blackboard key
// "TargetActor" = fan or player; grudge = a BT service on OnTakeAnyDamage with a cooldown
// decorator; the grudge cap = a token system on the mission subsystem.

using System.Collections.Generic;
using JUTPS;
using JUTPS.AI;
using UnityEngine;
using UnityEngine.AI;

namespace PIMLR.Missions.SaveTheFans
{
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    [AddComponentMenu("PIMLR/Save The Fans/Fan Aggro Brain")]
    public class FanAggroBrain : MonoBehaviour
    {
        static readonly string[] NoTargets = new string[0];
        static int spawnSerial; // (not GetInstanceID: instance IDs are on their way out in Unity 6.x)

        [Header("Assigned at spawn (leave empty on prefabs)")]
        public FanAggroProfile profile;

        [Header("Steering")]
        [Tooltip("How often the walk-to spot and the AI path are refreshed.")]
        [Min(0.1f)] public float repathSeconds = 0.35f;
        [Tooltip("While holding a grudge the enemy's senses are widened to at least this (same idea as PimlrHuntPlayer).")]
        public float grudgeViewRadius = 25f;
        public float grudgeViewAngle = 200f;

        // ---------------- runtime ----------------
        public FanHeat Fan { get; private set; }
        public bool HasGrudge => grudgeActive;
        public bool IsDown { get; private set; }

        SaveTheFansMission mission;
        Transform player;
        JUHealth health;
        JUCharacterArtificialInteligenceBrain brain;
        ZombieAI zombie;
        PatrolAI patrol;
        AIRandomDestination randomDestination;
        PimlrGunnerAI gunner;
        PimlrBomberAI bomber;
        PimlrHuntPlayer hunter;
        PimlrSeekIntersection seeker;

        string[] nativeTargetTags = NoTargets;
        float nativeRadius, nativeAngle;
        bool nativeCaptured;

        float washMultiplier = 1f;
        float slotAngleOffset;
        bool held, brainHeldOff;
        bool grudgeActive;
        float grudgeUntil;
        float lastHealth = -1f;
        bool tookDamageThisFrame;
        float nextRepath, nextSwing, swingUntil;
        bool refreshRateSet;
        Vector3 walkTarget;
        readonly Queue<(float time, float amount)> damageLog = new Queue<(float, float)>();

        public static bool IsMissionEnemy(GameObject go) => go && go.GetComponent<FanAggroBrain>() != null;

        /// Safe to call while the enemy is inactive (it is, while its arrival portal opens).
        public void Init(SaveTheFansMission owner, FanHeat fan, Transform playerTransform, FanAggroProfile aggro, float washRateMultiplier)
        {
            mission = owner;
            Fan = fan;
            player = playerTransform;
            profile = aggro;
            washMultiplier = washRateMultiplier;

            health = GetComponent<JUHealth>();
            brain = GetComponent<JUCharacterArtificialInteligenceBrain>();
            zombie = brain as ZombieAI;
            patrol = brain as PatrolAI;
            randomDestination = GetComponent<AIRandomDestination>();

            if (!brain)
                Debug.LogWarning($"[SaveTheFans] {name} has no JUTPS AI brain (ZombieAI / PatrolAI). It will count as a kill but won't be steered.", this);

            // Each enemy takes a slightly different spot beside the fan so a wave fans out instead of stacking.
            slotAngleOffset = ((spawnSerial++ % 7) - 3) * 14f;

            CaptureNative();
            lastHealth = health ? health.Health : -1f;
            ApplyFocus(onPlayer: false);
        }

        /// Freeze this enemy (used while the intro camera is showing it).
        public void SetHeld(bool hold)
        {
            held = hold;
            if (!hold && brainHeldOff && brain && !(health && health.IsDead))
            {
                brain.enabled = true;
                brainHeldOff = false;
            }
        }

        /// Mission cleanup (heat failed / aborted): remove without counting as a kill.
        public void Despawn()
        {
            if (!IsDown)
            {
                IsDown = true;
                if (grudgeActive && mission) mission.ReleaseGrudge(this);
                grudgeActive = false;
            }
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (mission == null || IsDown) return;

            if (health && (health.IsDead || health.Health <= 0f)) { ReportDown(); return; }

            if (held) { HoldStill(); return; }

            TrackDamage();

            // Freeze Ray: JUHealth switches the brain off while the enemy is frozen. Leave it alone.
            if (brain && !brain.enabled) return;

            UpdateGrudge();

            if (brain)
            {
                if (!refreshRateSet) { brain.SetPathCalculationRefreshRate(repathSeconds); refreshRateSet = true; }
                if (randomDestination && randomDestination.enabled) randomDestination.enabled = false;

                if (Time.time >= nextRepath)
                {
                    nextRepath = Time.time + repathSeconds;
                    EnforceFocus();
                    walkTarget = grudgeActive ? OnNavMesh(player.position) : FanSlot();
                }
                // Every frame, and after PatrolAI's own Update (see header).
                brain.Destination = walkTarget;
            }

            if (!grudgeActive) WashFanIfInRange();
        }

        void HoldStill()
        {
            if (!brain) return;
            if (brain.enabled) { brain.enabled = false; brainHeldOff = true; }
            if (brain.character) brain.character._Move(0f, 0f, false);
        }

        // ------------------------------------------------------------------ focus
        void CaptureNative()
        {
            if (nativeCaptured) return;
            if (zombie)
            {
                nativeTargetTags = zombie.TargetTags ?? NoTargets;
                if (zombie.FieldOfView != null) { nativeRadius = zombie.FieldOfView.Radious; nativeAngle = zombie.FieldOfView.Angle; }
                nativeCaptured = true;
            }
            else if (patrol)
            {
                nativeTargetTags = patrol.TargetTags ?? NoTargets;
                if (patrol.FieldOfView != null) { nativeRadius = patrol.FieldOfView.Radious; nativeAngle = patrol.FieldOfView.Angle; }
                nativeCaptured = true;
            }
        }

        void ApplyFocus(bool onPlayer)
        {
            string[] tags = onPlayer ? nativeTargetTags : NoTargets;
            float radius = onPlayer ? Mathf.Max(nativeRadius, grudgeViewRadius) : nativeRadius;
            float angle = onPlayer ? Mathf.Max(nativeAngle, grudgeViewAngle) : nativeAngle;

            if (zombie)
            {
                zombie.TargetTags = tags;
                if (zombie.FieldOfView != null) { zombie.FieldOfView.Radious = radius; zombie.FieldOfView.Angle = angle; }
            }
            else if (patrol)
            {
                patrol.TargetTags = tags;
                if (patrol.FieldOfView != null) { patrol.FieldOfView.Radious = radius; patrol.FieldOfView.Angle = angle; }
            }
            EnforceFocus();
            nextRepath = 0f;
        }

        /// The gunner/bomber movement scripts fight the player on their own terms. They run only
        /// while this enemy holds a grudge; while it's going for the fan they'd drag it off course.
        void EnforceFocus()
        {
            if (!gunner) gunner = GetComponent<PimlrGunnerAI>();
            if (!bomber) bomber = GetComponent<PimlrBomberAI>();
            if (!hunter) hunter = GetComponent<PimlrHuntPlayer>();
            if (!seeker) seeker = GetComponent<PimlrSeekIntersection>();

            if (gunner && gunner.enabled != grudgeActive) gunner.enabled = grudgeActive;
            if (bomber && bomber.enabled != grudgeActive) bomber.enabled = grudgeActive;
            if (hunter && hunter.enabled) hunter.enabled = false;   // this script does the steering
            if (seeker && seeker.enabled) seeker.enabled = false;
        }

        Vector3 FanSlot()
        {
            if (!Fan) return transform.position;
            Vector3 fanPos = Fan.transform.position;
            Vector3 fromFan = transform.position - fanPos;
            fromFan.y = 0f;
            if (fromFan.sqrMagnitude < 0.01f) fromFan = Vector3.forward;

            float standOff = Mathf.Max(1.2f, (profile ? profile.washRange : 2.5f) * 0.7f);
            Vector3 dir = Quaternion.AngleAxis(slotAngleOffset, Vector3.up) * fromFan.normalized;
            return OnNavMesh(fanPos + dir * standOff, fanPos);
        }

        static Vector3 OnNavMesh(Vector3 p) => OnNavMesh(p, p);
        static Vector3 OnNavMesh(Vector3 p, Vector3 fallback)
        {
            if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) return hit.position;
            return NavMesh.SamplePosition(fallback, out hit, 4f, NavMesh.AllAreas) ? hit.position : fallback;
        }

        // ------------------------------------------------------------------ grudge
        void TrackDamage()
        {
            tookDamageThisFrame = false;
            if (!health) return;
            float h = health.Health;
            if (lastHealth >= 0f && h < lastHealth - 0.01f)
            {
                damageLog.Enqueue((Time.time, lastHealth - h));
                tookDamageThisFrame = true;
            }
            lastHealth = h;

            float window = profile ? profile.damageWindow : 3f;
            while (damageLog.Count > 0 && Time.time - damageLog.Peek().time > window) damageLog.Dequeue();
        }

        void UpdateGrudge()
        {
            if (!profile || !player) return;

            if (grudgeActive)
            {
                if (tookDamageThisFrame) grudgeUntil = Time.time + profile.grudgeDuration; // more hits keep it angry

                if (Time.time >= grudgeUntil ||
                    FlatDistance(transform.position, player.position) > profile.grudgeBreakDistance ||
                    PimlrInput.PlayerInVehicle)
                    EndGrudge();
                return;
            }

            if (profile.focusMode != FanAggroProfile.FocusMode.Grudge || !health || damageLog.Count == 0) return;
            if (PimlrInput.PlayerInVehicle) return; // can't bait the wave from a bike

            float total = 0f;
            foreach (var d in damageLog) total += d.amount;

            if (total / Mathf.Max(1f, health.MaxHealth) >= profile.grudgeDamageThreshold && mission.TryAcquireGrudge(this))
            {
                grudgeActive = true;
                grudgeUntil = Time.time + profile.grudgeDuration;
                damageLog.Clear();
                ApplyFocus(onPlayer: true);
            }
        }

        void EndGrudge()
        {
            if (!grudgeActive) return;
            grudgeActive = false;
            if (mission) mission.ReleaseGrudge(this);
            ApplyFocus(onPlayer: false);
        }

        // ------------------------------------------------------------------ washing the fan
        void WashFanIfInRange()
        {
            if (!Fan || !profile) return;
            if (FlatDistance(transform.position, Fan.transform.position) > profile.washRange) return;

            Fan.AddWash(profile.washPerSecond * washMultiplier * Time.deltaTime);

            var character = brain ? brain.character : null;
            if (!character) return;

            if (profile.faceFanWhileWashing) character.DoLookAt(Fan.FocusPoint, 5f);

            if (profile.swingAtFan)
            {
                // Same input pattern ZombieAI.EnterAttackModeState uses.
                if (Time.time >= nextSwing)
                {
                    nextSwing = Time.time + profile.swingInterval;
                    swingUntil = Time.time + 0.25f;
                }
                bool swinging = Time.time < swingUntil;
                character.DefaultUseOfAllItems(swinging, swinging, false, false, false, true);
            }
        }

        // ------------------------------------------------------------------ death
        void ReportDown()
        {
            if (IsDown) return;
            IsDown = true;
            if (grudgeActive && mission) mission.ReleaseGrudge(this);
            grudgeActive = false;
            if (mission) mission.ReportEnemyDown(this);
        }

        void OnDisable()
        {
            // A body that is switched off (rather than left as a ragdoll) after dying.
            if (!IsDown && mission && health && health.IsDead) ReportDown();
        }

        void OnDestroy()
        {
            // Removed by something else without dying first (kill-plane, scene cleanup).
            if (!IsDown && mission) ReportDown();
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
