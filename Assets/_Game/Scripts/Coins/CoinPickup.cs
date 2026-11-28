// PIMLR World — Coin pickup (Unity 6.6 / URP)
// One coin in the world. Lifecycle:
//   Spilling  -> physics burst out of the dead enemy, bounces, settles
//   Idle      -> kinematic, spins + bobs, shiny
//   Magnet    -> player entered collect range: accelerates to the player
//   Collected -> deposits value, returns to the pool
//
// Unreal equivalent: APIMLRCoin (AActor) with UStaticMeshComponent (simulate physics
// while spilling), URotatingMovementComponent for idle spin, and a Tick-driven
// homing step toward the pawn. Pool = UObject pool in a UWorldSubsystem.
using System.Collections.Generic;
using UnityEngine;

namespace PIMLR.Coins
{
    [RequireComponent(typeof(Rigidbody))]
    public class CoinPickup : MonoBehaviour
    {
        public enum State { Spilling, Idle, Magnet, Collected }

        // Static registry: the collector loops this instead of physics queries,
        // so no "Coin" layer / layer-matrix setup is required.
        public static readonly List<CoinPickup> Active = new List<CoinPickup>(256);

        [Header("Value")]
        public int value = 1;

        [Header("Idle presentation")]
        [Tooltip("Degrees per second around world up.")]
        public float spinSpeed = 180f;
        public float bobHeight = 0.08f;
        public float bobSpeed = 2.2f;
        [Tooltip("Hover height above the ground once settled.")]
        public float hoverHeight = 0.35f;
        public LayerMask groundMask = ~0;

        [Header("Spill")]
        [Tooltip("Seconds after spawning before the player can magnet it (lets the burst read).")]
        public float pickupDelay = 0.45f;
        [Tooltip("Force settle after this long even if still rolling.")]
        public float maxSpillTime = 2.5f;
        public float settleSpeed = 0.35f;

        [Header("Magnet")]
        public float magnetStartSpeed = 4f;
        public float magnetAccel = 40f;
        public float collectDistance = 0.6f;

        [Header("Lifetime")]
        [Tooltip("0 = never despawn. Otherwise uncollected coins vanish after this many seconds.")]
        public float lifetime = 45f;

        [Header("FX (optional)")]
        public ParticleSystem sparkle;     // idle glint loop
        public AudioClip collectSfx;
        [Range(0, 1)] public float sfxVolume = 0.6f;

        public State CurrentState { get; private set; }
        public bool CanBeMagnetized => CurrentState == State.Idle ||
                                       (CurrentState == State.Spilling && _age >= pickupDelay);

        Rigidbody _rb;
        Collider _col;
        Transform _target;
        float _age, _magnetSpeed, _bobPhase, _idleY;
        System.Action<CoinPickup> _onCollected;
        System.Action<CoinPickup> _release;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        bool _reenableInterp;

        Vector3 _spillOrigin;
        float _spillRadius = -1f;

        /// Keep this coin within radius (horizontal) of where the enemy fell.
        public void SetSpillLimit(Vector3 origin, float radius) { _spillOrigin = origin; _spillRadius = radius; }

        Vector3 ClampToSpill(Vector3 p)
        {
            if (_spillRadius <= 0f) return p;
            Vector3 d = p - _spillOrigin; d.y = 0f;
            if (d.sqrMagnitude <= _spillRadius * _spillRadius) return p;
            Vector3 c = _spillOrigin + d.normalized * _spillRadius;
            c.y = p.y;
            return c;
        }

        void FixedUpdate()
        {
            if (CurrentState == State.Spilling && _spillRadius > 0f)
            {
                Vector3 p = _rb.position, c = ClampToSpill(p);
                if (c != p)
                {   // hit the edge of the spill area: stop drifting outward
                    _rb.position = c;
                    Vector3 v = _rb.linearVelocity, outward = (c - _spillOrigin); outward.y = 0f; outward.Normalize();
                    float o = Vector3.Dot(v, outward);
                    if (o > 0f) _rb.linearVelocity = v - outward * o * 1.4f;
                }
            }
            // turn smoothing back on after the teleport has been simulated once
            if (_reenableInterp) { _reenableInterp = false; _rb.interpolation = RigidbodyInterpolation.Interpolate; }
        }

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// Called by CoinSpawner when pulled from the pool.
        public void Launch(Vector3 position, Vector3 velocity, Vector3 angularVelocity,
                           int coinValue, System.Action<CoinPickup> release)
        {
            // Teleport the RIGIDBODY, not just the transform. An interpolated body overwrites
            // transform writes with its own pose, which for a pooled coin is the spawner's
            // position (scene origin) — that was the "coins spawn at origin" bug.
            var rot = Random.rotation;
            _rb.interpolation = RigidbodyInterpolation.None;
            _rb.isKinematic = false;
            _rb.position = position;
            _rb.rotation = rot;
            transform.SetPositionAndRotation(position, rot);
            _reenableInterp = true;
            value = coinValue;
            _release = release;
            _age = 0f;
            _target = null;
            _bobPhase = Random.value * Mathf.PI * 2f;

            if (_col) _col.enabled = true;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = velocity;          // Unity 6: linearVelocity replaces velocity
            _rb.angularVelocity = angularVelocity;
            CurrentState = State.Spilling;
            if (sparkle) sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// Collector calls this when the coin is within range.
        public void BeginMagnet(Transform target, System.Action<CoinPickup> onCollected)
        {
            if (!CanBeMagnetized) return;
            _target = target;
            _onCollected = onCollected;
            _magnetSpeed = magnetStartSpeed;
            _reenableInterp = false;
            _rb.interpolation = RigidbodyInterpolation.None;
            _rb.isKinematic = true;
            if (_col) _col.enabled = false;   // fly through everything
            CurrentState = State.Magnet;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (lifetime > 0f && _age > lifetime && CurrentState != State.Magnet) { Despawn(); return; }

            switch (CurrentState)
            {
                case State.Spilling:
                    if (_age > pickupDelay &&
                        (_rb.linearVelocity.sqrMagnitude < settleSpeed * settleSpeed || _age > maxSpillTime))
                        Settle();
                    break;

                case State.Idle:
                    transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                    var p = transform.position;
                    p.y = _idleY + Mathf.Sin(Time.time * bobSpeed + _bobPhase) * bobHeight;
                    transform.position = p;
                    break;

                case State.Magnet:
                    if (!_target) { Settle(); break; }
                    Vector3 aim = _target.position + Vector3.up * 1.0f; // chest height
                    _magnetSpeed += magnetAccel * Time.deltaTime;
                    transform.position = Vector3.MoveTowards(transform.position, aim, _magnetSpeed * Time.deltaTime);
                    transform.Rotate(Vector3.up, spinSpeed * 3f * Time.deltaTime, Space.World);
                    if ((transform.position - aim).sqrMagnitude < collectDistance * collectDistance)
                        Collect();
                    break;
            }
        }

        void Settle()
        {
            transform.position = ClampToSpill(transform.position);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            // from here on the transform is driven by code (bob/spin/magnet), so stop
            // interpolation from pulling it back to the last physics pose
            _reenableInterp = false;
            _rb.interpolation = RigidbodyInterpolation.None;

            // Snap upright (on its edge, facing a random direction) and hover above ground
            Vector3 pos = transform.position;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y + hoverHeight;
            _idleY = pos.y;
            // Mesh faces +Y when imported from Blender; stand it on edge so the logo is visible
            transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
            CurrentState = State.Idle;
            if (sparkle) sparkle.Play(true);
        }

        void Collect()
        {
            CurrentState = State.Collected;
            if (collectSfx) AudioSource.PlayClipAtPoint(collectSfx, transform.position, sfxVolume);
            _onCollected?.Invoke(this);
            Despawn();
        }

        void Despawn()
        {
            CurrentState = State.Collected;
            if (_release != null) _release(this);
            else gameObject.SetActive(false);
        }
    }
}
