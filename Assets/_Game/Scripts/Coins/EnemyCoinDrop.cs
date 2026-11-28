// PIMLR World — Enemy coin drop
// Add to each PIMLR_ enemy prefab (PIMLR_Melee, PIMLR_Gunner, PIMLR_Bomber).
// Auto-hooks JUTPS JUHealth death (no hard compile dependency on JUTPS names —
// it binds OnDeath if present and also watches IsDead as a fallback).
// You can also wire DropCoins() manually from any UnityEvent in the Inspector.
//
// Unreal equivalent: UPIMLRCoinDropComponent (UActorComponent) bound to the enemy's
// health component OnDeath delegate; calls the coin subsystem's Spill().
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace PIMLR.Coins
{
    public class EnemyCoinDrop : MonoBehaviour
    {
        [Header("Payout")]
        public int minValue = 3;
        public int maxValue = 6;
        [Tooltip("Visible coins per drop. -1 = one coin per value point (capped at 12).")]
        public int coinCount = -1;
        [Range(0, 1)] public float dropChance = 1f;
        [Tooltip("Payout multiplier once Zone 2 has started (the old kill reward was 5 in Zone 1, 10 in Zone 2).")]
        public float zone2Multiplier = 2f;

        [Header("Where")]
        [Tooltip("Optional: spill from here (e.g. chest bone). Defaults to this transform.")]
        public Transform dropPoint;
        [Tooltip("Delay so coins burst with the robot death FX rather than before it.")]
        public float delay = 0.25f;

        [Header("Hook")]
        [Tooltip("Health component to watch. Leave empty to auto-find a component named 'JUHealth'.")]
        public Component health;

        bool _dropped;
        MemberInfo _isDeadMember;

        void Awake()
        {
            if (!health)
                foreach (var c in GetComponentsInChildren<Component>(true))
                    if (c && c.GetType().Name == "JUHealth") { health = c; break; }

            if (!health) return;
            var t = health.GetType();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // Prefer the event
            var onDeath = t.GetField("OnDeath", F)?.GetValue(health) as UnityEvent;
            if (onDeath == null) onDeath = t.GetProperty("OnDeath", F)?.GetValue(health) as UnityEvent;
            onDeath?.AddListener(DropCoins);

            // Fallback watcher
            _isDeadMember = (MemberInfo)t.GetField("IsDead", F) ?? t.GetProperty("IsDead", F);
        }

        void OnEnable() { _dropped = false; }   // pooled / respawned enemies (Infinite Mode waves)

        void Update()
        {
            if (_dropped || _isDeadMember == null) return;
            object v = _isDeadMember is FieldInfo fi ? fi.GetValue(health)
                     : ((PropertyInfo)_isDeadMember).GetValue(health);
            if (v is bool dead && dead) DropCoins();
        }

        public void DropCoins()
        {
            if (_dropped) return;
            _dropped = true;
            if (TutorialDirector.IsRunning) return;   // PIMLR #TUT: warm-up robot pays nothing
            if (Random.value > dropChance) return;
            if (!CoinSpawner.Instance)
            {
                Debug.LogWarning("[PIMLR Coins] No CoinSpawner in scene — add one to YannicksWorld.", this);
                return;
            }
            // Capture position now: the enemy may be destroyed by its death FX before the delay ends.
            var p = ResolveDropPosition();
            if (debugLog) Debug.Log($"[PIMLR Coins] {name} died at {p} (root at {transform.position})", this);
            int value = Random.Range(minValue, maxValue + 1);
            var gem = GameExecutionManager.Instance;
            if (gem && gem.zone2Start) value = Mathf.RoundToInt(value * zone2Multiplier);
            value = Mathf.RoundToInt(value * PimlrBoostEffects.CoinMultiplier); // PIMLR (Nov 27): Track Boost "2x PIMLR Coins"
            CoinSpawner.Instance.Spill(p, value, coinCount, delay);
        }

        [Header("Debug")]
        public bool debugLog = false;
        public LayerMask groundMask = ~0;

        /// Where the enemy's BODY actually is — not the prefab root, which may stay at the
        /// spawn point while a child (JUTPS character / ragdoll) does the moving.
        Vector3 ResolveDropPosition()
        {
            Transform src = dropPoint;
            if (!src)
            {
                var anim = GetComponentInChildren<Animator>();
                if (anim && anim.isHuman) src = anim.GetBoneTransform(HumanBodyBones.Hips);
            }
            if (!src && health) src = health.transform;
            Vector3 p = src ? src.position : transform.position;

            if (!src)
            {   // last resort: centre of the visible mesh
                var r = GetComponentInChildren<Renderer>();
                if (r) p = r.bounds.center;
            }

            // drop onto the ground under the body; spawner adds its spawnHeight
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 5f, groundMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
                p.y = hit.point.y;
            return p;
        }
    }
}
