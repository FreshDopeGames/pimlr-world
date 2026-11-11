// PIMLR #STF (Save The Fans) — put this on each fan NPC's root.
// Owns: the fan's "wash meter" (0 = fresh, max = washed out = heat failed), its spawn
// points, its golden ring + minimap blip, nearby ammo restock stations and reactions.
//
// Why a wash meter instead of JUHealth on the fan:
//   If the fan had JUHealth + a "Player"-like tag, the JUTPS AI's own detection/attack
//   code would start treating it unpredictably (and player splash damage could hurt it).
//   A separate meter that enemies fill by standing near the fan is deterministic, easy to
//   tune, and immune to friendly fire. Enemies still shoot the PLAYER with their normal weapons.
//
// Unreal note: AFanHeatActor (or a UFanHeatComponent on the NPC) with a float WashMeter
// UPROPERTY and FOnFanWashChanged / FOnFanWashedOut dynamic multicast delegates.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace PIMLR.Missions.SaveTheFans
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PIMLR/Save The Fans/Fan Heat")]
    public class FanHeat : MonoBehaviour
    {
        [Header("Identity")]
        public string fanDisplayName = "Fan";

        [Header("Waves")]
        public FanWaveSet waveSet;

        [Tooltip("Where this heat's enemies come from. Use PIMLR > Save The Fans > Auto-Place Spawn Points to generate valid ones.")]
        public Transform[] spawnPoints = new Transform[0];

        [Header("Arrival")]
        [Tooltip("The heat starts when the player gets within this distance of the fan.")]
        [Min(2f)] public float arrivalRadius = 12f;

        [Tooltip("Seconds the player gets to travel here from the previous fan. 0 = use the mission's default. Validate All Heats suggests a value from the real route length.")]
        [Min(0f)] public float travelSeconds = 0f;

        [Tooltip("Optional point the camera looks at (e.g. the fan's head). Defaults to the fan + 1.6m.")]
        public Transform cameraFocus;

        [Header("Wash meter")]
        [Min(1f)] public float maxWash = 100f;

        [Header("Presentation (all optional)")]
        public FanGlowRing ring;
        public FanMinimapBlip minimapBlip;
        public AmmoRestockStation[] restockStations = new AmmoRestockStation[0];

        [Tooltip("Optional particles on the fan that play while enemies are washing them (e.g. suds).")]
        public ParticleSystem washFx;

        [Tooltip("Fan animator. Triggers below are only fired if the parameter exists.")]
        public Animator animator;
        public string cowerTrigger = "Cower";
        public string cheerTrigger = "Cheer";
        public string washedTrigger = "Washed";

        [Header("Events")]
        public UnityEvent onHighlighted;
        public UnityEvent onHeatStarted;
        public UnityEvent onFanSaved;
        public UnityEvent onFanWashedOut;

        // ---------------- runtime ----------------
        public float Wash { get; private set; }
        public float Wash01 => maxWash <= 0f ? 0f : Mathf.Clamp01(Wash / maxWash);
        public bool IsWashedOut => Wash >= maxWash;

        /// True during the intro cinematic so the fan can't be washed while the camera is away.
        public bool Invulnerable { get; set; }

        /// 0..1. Set by Fan Hype; 0.6 means the fan only takes 40% of incoming wash.
        public float WashResistance { get; set; }

        public event Action<FanHeat> WashedOut;
        public event Action<FanHeat, float> WashChanged;

        float lastCowerTime = -99f;
        float lastWashTime = -99f;
        const float CowerCooldown = 2.5f;

        public Vector3 FocusPoint => cameraFocus ? cameraFocus.position : transform.position + Vector3.up * 1.6f;

        void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            ring = GetComponentInChildren<FanGlowRing>(true);
            minimapBlip = GetComponentInChildren<FanMinimapBlip>(true);
        }

        void Awake()
        {
            SetHighlighted(false);
            SetRestockActive(false);
        }

        public void ResetWash()
        {
            Wash = 0f;
            WashResistance = 0f;
            WashChanged?.Invoke(this, Wash01);
            if (ring) ring.SetDanger(0f);
        }

        /// Called by enemies every frame they're in range. amount is already * deltaTime.
        public void AddWash(float amount)
        {
            if (Invulnerable || IsWashedOut || amount <= 0f) return;

            Wash = Mathf.Min(maxWash, Wash + amount * (1f - Mathf.Clamp01(WashResistance)));
            lastWashTime = Time.time;
            WashChanged?.Invoke(this, Wash01);
            if (ring) ring.SetDanger(Wash01);

            if (Time.time - lastCowerTime > CowerCooldown)
            {
                lastCowerTime = Time.time;
                FireTrigger(cowerTrigger);
            }

            if (IsWashedOut)
            {
                FireTrigger(washedTrigger);
                onFanWashedOut?.Invoke();
                WashedOut?.Invoke(this);
            }
        }

        void Update()
        {
            if (!washFx) return;
            bool beingWashed = Time.time - lastWashTime < 0.4f && !IsWashedOut;
            if (beingWashed && !washFx.isPlaying) washFx.Play(true);
            else if (!beingWashed && washFx.isPlaying) washFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Recover(float amount)
        {
            if (amount <= 0f || IsWashedOut) return;
            Wash = Mathf.Max(0f, Wash - amount);
            WashChanged?.Invoke(this, Wash01);
            if (ring) ring.SetDanger(Wash01);
        }

        /// Golden ring + minimap blip on/off. 'travelBeacon' also shows the tall beam used to find the fan across the city.
        public void SetHighlighted(bool on, bool travelBeacon = false)
        {
            if (ring)
            {
                ring.SetVisible(on);
                ring.SetBeacon(on && travelBeacon);
                if (on) ring.SetDanger(Wash01);
            }
            if (minimapBlip) minimapBlip.SetVisible(on);
            if (on) onHighlighted?.Invoke();
        }

        public void SetRestockActive(bool on)
        {
            if (restockStations == null) return;
            foreach (var s in restockStations) if (s) s.SetActive(on);
        }

        public void NotifyHeatStarted() => onHeatStarted?.Invoke();

        public void NotifySaved()
        {
            FireTrigger(cheerTrigger);
            if (ring) ring.Celebrate();
            onFanSaved?.Invoke();
        }

        void FireTrigger(string param)
        {
            if (!animator || string.IsNullOrEmpty(param) || !animator.isActiveAndEnabled) return;
            foreach (var p in animator.parameters)
                if (p.name == param && p.type == AnimatorControllerParameterType.Trigger) { animator.SetTrigger(param); return; }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            DrawCircle(transform.position, arrivalRadius);

            if (spawnPoints == null) return;
            foreach (var sp in spawnPoints)
            {
                if (!sp) continue;
                Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
                Gizmos.DrawWireSphere(sp.position, 0.75f);
                Gizmos.DrawLine(sp.position + Vector3.up * 0.2f, transform.position + Vector3.up * 0.2f);
            }
        }

        static void DrawCircle(Vector3 c, float r)
        {
            const int seg = 48;
            Vector3 prev = c + new Vector3(r, 0.1f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0.1f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
