// PIMLR World — Coin tally HUD
// Put on a TextMeshProUGUI in the gameplay HUD canvas. Shows the live balance (including
// coins not yet flushed to CoinManager) with a count-up, a punch scale, and an optional
// "+N" popup that accumulates while coins are streaming in.
//
// Unreal equivalent: UMG widget bound to UPIMLRCurrencySubsystem::OnCoinValueChanged,
// with a widget Animation for the punch and an FInterpTo count-up in NativeTick.
using TMPro;
using UnityEngine;

namespace PIMLR.Coins
{
    public class CoinTallyHUD : MonoBehaviour
    {
        public TextMeshProUGUI balanceText;
        [Tooltip("Optional '+N' label beside the balance.")]
        public TextMeshProUGUI gainText;
        public string format = "{0:N0}";
        public float countUpSpeed = 12f;
        public float punchScale = 1.25f;
        public float gainHoldTime = 1.2f;

        float _shown, _target, _punch, _gainTimer;
        int _gainAccum;
        Vector3 _baseScale;

        void Awake()
        {
            if (!balanceText) balanceText = GetComponent<TextMeshProUGUI>();
            _baseScale = balanceText.rectTransform.localScale;
            if (CoinManager.Instance != null) _shown = _target = CoinManager.Instance.GetCoins();
            Refresh();
            if (gainText) gainText.alpha = 0f;
        }

        void OnEnable() { PIMLRCoinBank.OnDisplayedBalanceChanged += OnBalance; }
        void OnDisable() { PIMLRCoinBank.OnDisplayedBalanceChanged -= OnBalance; }

        void OnBalance(int newBalance)
        {
            int delta = Mathf.Max(0, newBalance - Mathf.RoundToInt(_target));
            _target = newBalance;
            _punch = 1f;
            _gainAccum += delta;
            _gainTimer = gainHoldTime;
        }

        void Update()
        {
            if (!Mathf.Approximately(_shown, _target))
            {
                _shown = Mathf.MoveTowards(_shown, _target,
                    Mathf.Max(1f, Mathf.Abs(_target - _shown)) * countUpSpeed * Time.unscaledDeltaTime);
                Refresh();
            }
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 4f);
            balanceText.rectTransform.localScale = _baseScale * (1f + (punchScale - 1f) * _punch);

            if (gainText)
            {
                if (_gainTimer > 0f)
                {
                    _gainTimer -= Time.unscaledDeltaTime;
                    gainText.text = "+" + _gainAccum;
                    gainText.alpha = Mathf.Clamp01(_gainTimer / 0.3f);
                    if (_gainTimer <= 0f) _gainAccum = 0;
                }
            }
        }

        void Refresh() => balanceText.text = string.Format(format, Mathf.RoundToInt(_shown));
    }
}
