using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PIMLR: keeps a PIMLR Coin balance label in sync with CoinManager, by itself. The HUD (Menu/Bg/CoinTXT), the
/// pause screen ("PIMLR Coins/CoinTXT", "Dollar/Coins") and the menus each had their own text with nothing (or a
/// lost Inspector reference) updating it, so they sat at 0. Installed on every scene load by PimlrInputRuntime on
/// any text with one of the known coin-label names; add it by hand to any new coin label.
/// </summary>
public class PimlrCoinLabel : MonoBehaviour
{
    // Object names of coin balance labels (the label itself, or its parent for "Coin_bg" style badges).
    static readonly string[] LabelNames = { "CoinTXT", "CoinText", "Coin Text" };
    static readonly string[] BadgeNames = { "Coin_bg" };

    TMP_Text tmp;
    Text legacy;
    CoinManager bound;

    public static void InstallAll()
    {
        int n = 0;
        foreach (var t in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject target = null;
            if (System.Array.IndexOf(LabelNames, t.name) >= 0 && HasText(t.gameObject)) target = t.gameObject;
            // Pause screen: ".../Dollar/Coins" (a generic name, so only under the Dollar icon - not column headers).
            else if (t.name == "Coins" && t.parent != null && t.parent.name == "Dollar" && HasText(t.gameObject)) target = t.gameObject;
            else if (System.Array.IndexOf(BadgeNames, t.name) >= 0)
            {
                var label = t.GetComponentInChildren<TMP_Text>(true);
                if (label) target = label.gameObject;
            }
            if (target && !target.GetComponent<PimlrCoinLabel>()) { target.AddComponent<PimlrCoinLabel>(); n++; }
        }
        if (n > 0) Debug.Log($"PIMLR: bound {n} coin label(s) to CoinManager.");
    }

    static bool HasText(GameObject go) => go.GetComponent<TMP_Text>() || go.GetComponent<Text>();

    void Awake()
    {
        tmp = GetComponent<TMP_Text>();
        legacy = GetComponent<Text>();
    }

    void OnEnable()
    {
        Bind();
        Refresh();
    }

    void OnDisable()
    {
        if (bound != null) bound.coinValueChanged -= Show;
        bound = null;
    }

    // CoinManager may be created after this label (or replaced on a scene change) - keep checking cheaply.
    void Update()
    {
        if (bound == null || bound != CoinManager.Instance) { Bind(); Refresh(); }
    }

    void Bind()
    {
        var cm = CoinManager.Instance;
        if (cm == bound) return;
        if (bound != null) bound.coinValueChanged -= Show;
        bound = cm;
        if (bound != null) bound.coinValueChanged += Show;
    }

    void Refresh()
    {
        if (CoinManager.Instance != null) Show(CoinManager.Instance.GetCoins());
    }

    void Show(int value)
    {
        string s = value.ToString();
        if (tmp) tmp.text = s;
        if (legacy) legacy.text = s;
    }
}
