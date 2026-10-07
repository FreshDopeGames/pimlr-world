using UnityEngine;
using System;
using UnityEngine.UI;

public class CoinManager : Singleton<CoinManager>
{
    // PIMLR (playtest): keep the locally persisted balance as the single runtime value.
    private int coinValue;
    public Text coinText;

    internal Action<int> coinValueChanged;

    // PIMLR (playtest): load and publish the local balance after establishing the singleton.
    public override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        SetCoins(PlayerPrefs.GetInt(SessionState.CoinsKey, 0));
    }

    // PIMLR (playtest): persist a nonnegative balance before notifying UI and listeners.
    public void SetCoins(int newcoins)
    {
        coinValue = Mathf.Max(0, newcoins);
        PlayerPrefs.SetInt(SessionState.CoinsKey, coinValue);
        PlayerPrefs.Save();

        coinValueChanged?.Invoke(coinValue);
        if (coinText != null)
            coinText.text = coinValue.ToString();

        if (SceneManagerScript.Instance != null && SceneManagerScript.Instance._coins != null)
            SceneManagerScript.Instance._coins.text = coinValue.ToString();
    }

    // PIMLR (playtest): expose the currently loaded balance to existing callers.
    public int GetCoins() => coinValue;
}
