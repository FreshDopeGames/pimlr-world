// PIMLR World — Coin spawner + pool
// Drop one in YannicksWorld (e.g. on the GameExecutionManager object or an empty "Coins").
// Enemies call CoinSpawner.Instance.Spill(position, totalValue).
//
// Unreal equivalent: UPIMLRCoinSubsystem : UWorldSubsystem with a TArray<APIMLRCoin*> pool,
// Spill() as a BlueprintCallable. World (not GameInstance) subsystem because coins are per-level.
using UnityEngine;
using UnityEngine.Pool;

namespace PIMLR.Coins
{
    public class CoinSpawner : MonoBehaviour
    {
        public static CoinSpawner Instance { get; private set; }

        [Tooltip("Prefab with CoinPickup + Rigidbody + Collider (built by PIMLR > Coins > Build Coin Prefab).")]
        public CoinPickup coinPrefab;

        [Header("Pool")]
        public int prewarm = 60;
        public int maxPooled = 300;
        [Tooltip("Hard cap of coins alive at once; above this, spills merge into fewer, higher-value coins.")]
        public int maxAlive = 150;

        [Header("Spill feel")]
        public float upwardSpeed = 5.5f;
        public float outwardSpeed = 3.0f;
        public float spawnHeight = 1.1f;
        [Tooltip("Coins never end up further than this from where the enemy fell (metres).")]
        public float spillRadius = 1.6f;
        public float spinImpulse = 15f;
        [Tooltip("Small stagger so the burst 'pours' instead of popping all at once.")]
        public float perCoinStagger = 0.02f;

        ObjectPool<CoinPickup> _pool;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;   // per-scene, intentionally NOT DontDestroyOnLoad

            _pool = new ObjectPool<CoinPickup>(
                createFunc: () => { var c = Instantiate(coinPrefab, transform); c.gameObject.SetActive(false); return c; },
                actionOnGet: c => c.gameObject.SetActive(true),
                actionOnRelease: c => c.gameObject.SetActive(false),
                actionOnDestroy: c => { if (c) Destroy(c.gameObject); },
                collectionCheck: false, defaultCapacity: prewarm, maxSize: maxPooled);

            var warm = new CoinPickup[prewarm];
            for (int i = 0; i < prewarm; i++) warm[i] = _pool.Get();
            for (int i = 0; i < prewarm; i++) _pool.Release(warm[i]);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Spill coins worth totalValue at origin. coinCount is how many visible coins to split it into.</summary>
        public void Spill(Vector3 origin, int totalValue, int coinCount = -1, float delay = 0f)
        {
            if (delay > 0f) { StartCoroutine(DelayedSpill(origin, totalValue, coinCount, delay)); return; }
            if (!coinPrefab || totalValue <= 0) return;
            if (coinCount <= 0) coinCount = Mathf.Clamp(totalValue, 1, 12);

            int room = Mathf.Max(1, maxAlive - CoinPickup.Active.Count);
            coinCount = Mathf.Clamp(coinCount, 1, room);

            int baseValue = totalValue / coinCount;
            int remainder = totalValue % coinCount;
            StartCoroutine(SpillRoutine(origin, coinCount, baseValue, remainder));
        }

        System.Collections.IEnumerator DelayedSpill(Vector3 origin, int totalValue, int coinCount, float delay)
        {
            yield return new WaitForSeconds(delay);
            Spill(origin, totalValue, coinCount, 0f);
        }

        System.Collections.IEnumerator SpillRoutine(Vector3 origin, int count, int baseValue, int remainder)
        {
            var wait = perCoinStagger > 0 ? new WaitForSeconds(perCoinStagger) : null;
            for (int i = 0; i < count; i++)
            {
                int v = baseValue + (i < remainder ? 1 : 0);
                if (v <= 0) continue;

                float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                Vector3 outDir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                // pick a landing distance inside the radius, then the sideways speed that reaches it
                float up = Mathf.Min(upwardSpeed, 4f) * Random.Range(0.85f, 1.1f);
                float airTime = 2f * up / Mathf.Max(0.1f, -Physics.gravity.y) + 0.15f;
                float landDist = spillRadius * Mathf.Sqrt(Random.Range(0.15f, 0.8f));
                Vector3 vel = outDir * (landDist / airTime) + Vector3.up * up;

                var coin = _pool.Get();
                coin.Launch(origin + Vector3.up * spawnHeight + outDir * 0.15f, vel,
                            Random.insideUnitSphere * spinImpulse, v, Release);
                coin.SetSpillLimit(origin, spillRadius);
                if (wait != null) yield return wait;
            }
        }

        void Release(CoinPickup c) { if (c && c.gameObject.activeSelf) _pool.Release(c); }
    }
}
