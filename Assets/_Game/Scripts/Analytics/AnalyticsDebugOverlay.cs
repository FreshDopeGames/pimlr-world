using System.Collections.Generic;
using UnityEngine;

// PIMLR #ANL: dev-only live view of the last 20 analytics events, for checking new hooks.
// Only added in the Editor and Development builds. Toggle with F8.
namespace PIMLR.Analytics
{
    public class AnalyticsDebugOverlay : MonoBehaviour
    {
        const int Keep = 20;
        readonly Queue<string> lines = new Queue<string>(Keep);
        bool visible;
        GUIStyle style;

        void OnEnable() => PimlrAnalytics.EventLogged += OnLine;
        void OnDisable() => PimlrAnalytics.EventLogged -= OnLine;

        void OnLine(string json)
        {
            // Trim the envelope so the overlay shows "ev  d" only.
            int ev = json.IndexOf("\"ev\":", System.StringComparison.Ordinal);
            string shortLine = ev >= 0 ? json.Substring(ev) : json;
            if (shortLine.Length > 180) shortLine = shortLine.Substring(0, 180) + "…";
            if (lines.Count >= Keep) lines.Dequeue();
            lines.Enqueue(Time.realtimeSinceStartup.ToString("0.0") + "  " + shortLine);
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false, normal = { textColor = Color.white } };

            float h = 22 + lines.Count * 16;
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(8, 8, Screen.width * 0.7f, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(14, 10, 800, 18),
                $"PIMLR Analytics (F8)  consent:{PimlrAnalytics.Consent}  run:{(string.IsNullOrEmpty(PimlrAnalytics.RunId) ? "-" : PimlrAnalytics.RunId.Substring(0, 6))}", style);
            int i = 0;
            foreach (var l in lines)
                GUI.Label(new Rect(14, 28 + 16 * i++, Screen.width * 0.7f - 12, 16), l, style);
        }
    }
}
