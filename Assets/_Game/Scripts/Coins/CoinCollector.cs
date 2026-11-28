// PIMLR World — Player coin collector
// Add to the player root (the object with JUCharacterController).
// Every coin inside collectRadius gets magnetized to the player; on arrival its value
// is tallied into PIMLRCoinBank (which batches writes into CoinManager).
//
// Unreal equivalent: UPIMLRCoinCollectorComponent on the player Character, with a
// USphereComponent (overlap-only) for range; OnComponentBeginOverlap -> coin->BeginMagnet().
using UnityEngine;
using UnityEngine.Events;

namespace PIMLR.Coins
{
    public class CoinCollector : MonoBehaviour
    {
        [Tooltip("Magnet range in meters. Can be buffed by music tracks / achievements (see PowerChange).")]
        public float collectRadius = 4f;
        [Tooltip("Where coins fly to. Defaults to this transform.")]
        public Transform collectTarget;

        [Header("Events")]
        [Tooltip("Fires per coin collected with its value (hook HUD pop, SFX, haptics).")]
        public UnityEvent<int> onCoinCollected;

        public int SessionTotal { get; private set; }

        void Awake() { if (!collectTarget) collectTarget = transform; }

        void Update()
        {
            float r2 = collectRadius * collectRadius;
            Vector3 me = transform.position;
            var list = CoinPickup.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var c = list[i];
                if (!c || !c.CanBeMagnetized) continue;
                if ((c.transform.position - me).sqrMagnitude <= r2)
                    c.BeginMagnet(collectTarget, OnArrived);
            }
        }

        void OnArrived(CoinPickup coin)
        {
            SessionTotal += coin.value;
            PIMLRCoinBank.Deposit(coin.value);
            RunStats.AddCoins(coin.value);   // run summary / leaderboard count only collected coins
            onCoinCollected?.Invoke(coin.value);
        }

        /// Hook for AuthManager.PowerChange()-style buffs.
        public void MultiplyRadius(float factor) => collectRadius *= factor;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, collectRadius);
        }
#endif
    }
}
