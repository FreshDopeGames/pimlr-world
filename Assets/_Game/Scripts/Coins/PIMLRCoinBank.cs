// PIMLR World — Coin bank (adapter to the existing CoinManager)
// Why batching: CoinManager.SetCoins() persists via PlayerPrefs and may also POST to the
// idea-labs coin API. A 12-coin spill would otherwise trigger 12 saves/requests in a second.
// Deposits are buffered and flushed once per flushInterval (and on scene unload / quit).
//
// >>> If CoinManager's API differs from GetCoins()/SetCoins(int) + Instance,
// >>> edit ONLY the two lines inside Commit(). Nothing else references CoinManager.
//
// Unreal equivalent: the flush lives in UPIMLRCurrencySubsystem (GameInstanceSubsystem):
// AddCoins() accumulates, an FTimerHandle flushes to the USaveGame + broadcasts
// OnCoinValueChanged (dynamic multicast delegate) for UMG.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PIMLR.Coins
{
    public class PIMLRCoinBank : MonoBehaviour
    {
        public static float flushInterval = 0.5f;

        /// Fires immediately per deposit with (pendingTotalIncludingUnflushed) so HUD feels instant.
        public static event System.Action<int> OnDisplayedBalanceChanged;

        static PIMLRCoinBank _runner;
        static int _pending;
        float _timer;

        public static int Pending => _pending;

        public static void Deposit(int amount)
        {
            if (amount <= 0) return;
            EnsureRunner();
            _pending += amount;
            OnDisplayedBalanceChanged?.Invoke(ReadBalance() + _pending);
        }

        public static void FlushNow()
        {
            if (_pending == 0) return;
            int amt = _pending;
            _pending = 0;
            Commit(amt);
        }

        // ---------- the only CoinManager touchpoint ----------
        static int ReadBalance()
        {
            return CoinManager.Instance != null ? CoinManager.Instance.GetCoins() : 0;
        }

        static void Commit(int amount)
        {
            if (CoinManager.Instance == null) { _pending += amount; return; }
            CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() + amount); // fires coinValueChanged
        }
        // ------------------------------------------------------

        static void EnsureRunner()
        {
            if (_runner) return;
            var go = new GameObject("[PIMLRCoinBank]");
            DontDestroyOnLoad(go);
            _runner = go.AddComponent<PIMLRCoinBank>();
            SceneManager.sceneUnloaded += _ => FlushNow();
        }

        void Update()
        {
            if (_pending == 0) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer >= flushInterval) { _timer = 0f; FlushNow(); }
        }

        void OnApplicationPause(bool paused) { if (paused) FlushNow(); }
        void OnApplicationQuit() { FlushNow(); }
    }
}
