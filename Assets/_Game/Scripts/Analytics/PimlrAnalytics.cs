using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// PIMLR #ANL: the only analytics API. Callers do PimlrAnalytics.Log("kill", AnalyticsData.With("enemy", name)).
// Nothing here talks to a server: AnalyticsWriter appends JSON Lines to persistentDataPath/analytics/.
// Every line carries the common envelope (v, t, st, sid, rid, pid, build, plat, input, mode, scene, artist, ev, d).
// pid is PlayerProfile.AnonymousId. DisplayName must NEVER be logged.
//
// Unreal: UGameInstanceSubsystem writing the same JSON Lines with FFileHelper; same event names and fields.
namespace PIMLR.Analytics
{
    public static class PimlrAnalytics
    {
        public const int SchemaVersion = 1;

        // PIMLR #ANL: consent for sharing gameplay data with partners. ON by default while this is true (playtest
        // builds). Flip to false before a public release so players opt in from Settings instead.
        public const bool PlaytestConsentDefault = true;
        const string ConsentKey = "PIMLR_AnalyticsConsent";

        public static string SessionId { get; private set; } = NewId();
        public static string RunId { get; private set; } = "";
        static float sessionStartRealtime;

        /// <summary>Selected artist id. Phase 2 (Artist Roster) sets this from PlayerLoadout.</summary>
        public static string Artist = "thurz";

        /// <summary>Raised with each finished JSON line (debug overlay, tests).</summary>
        public static event Action<string> EventLogged;

        public static bool Consent
        {
            get => PlayerPrefs.GetInt(ConsentKey, PlaytestConsentDefault ? 1 : 0) == 1;
            set
            {
                bool was = Consent;
                PlayerPrefs.SetInt(ConsentKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (was != value)
                    LogForced("setting_change", AnalyticsData.With("key", "analytics_consent").Add("old", was).Add("new", value));
            }
        }

        public static string Folder => Path.Combine(Application.persistentDataPath, "analytics");

        internal static void ResetSession()
        {
            SessionId = NewId();
            RunId = "";
            sessionStartRealtime = Time.realtimeSinceStartup;
        }

        internal static void BeginRun() => RunId = NewId();
        internal static void EndRun() => RunId = "";

        public static void Log(string ev, AnalyticsData d = null)
        {
            if (!Consent) return;
            LogForced(ev, d);
        }

        // Consent changes themselves are always recorded so the file shows when logging stopped/started.
        static void LogForced(string ev, AnalyticsData d)
        {
            string line = BuildLine(ev, d);
            AnalyticsWriter.Enqueue(line);
            EventLogged?.Invoke(line);
        }

        static string BuildLine(string ev, AnalyticsData d)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            Json.Field(sb, "v", SchemaVersion, true);
            Json.Field(sb, "t", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
            Json.Field(sb, "st", Math.Round(Time.realtimeSinceStartup - sessionStartRealtime, 2));
            Json.Field(sb, "sid", SessionId);
            Json.Field(sb, "rid", RunId);
            Json.Field(sb, "pid", PlayerProfile.AnonymousId);
            Json.Field(sb, "build", Application.version);
            Json.Field(sb, "plat", Application.platform.ToString());
            Json.Field(sb, "input", PimlrInput.UsingGamepad ? "Gamepad" : "KeyboardMouse");
            Json.Field(sb, "mode", CurrentMode());
            Json.Field(sb, "scene", SceneManager.GetActiveScene().name);
            Json.Field(sb, "artist", Artist);
            Json.Field(sb, "ev", ev);
            sb.Append(",\"d\":");
            Json.Write(sb, d ?? AnalyticsData.Empty);
            sb.Append('}');
            return sb.ToString();
        }

        static string CurrentMode()
        {
            if (RunStats.Active) return RunStats.Mode.ToString();
            string scene = SceneManager.GetActiveScene().name;
            if (scene == "SceneStaticEU") return "Hub";
            if (scene == "00_MainMenu") return "Menu";
            return "None";
        }

        static string NewId() => Guid.NewGuid().ToString("N");

        // ------------------------------------------------------------------ getting the files back

        /// <summary>Desktop: opens the analytics folder in Explorer / Finder.</summary>
        public static void OpenFolder()
        {
            AnalyticsWriter.FlushNow();
            Directory.CreateDirectory(Folder);
            Application.OpenURL("file:///" + Folder.Replace('\\', '/'));
        }

        /// <summary>Desktop: zips the analytics folder next to it and returns the zip path (null on WebGL).</summary>
        public static string ExportZip()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DownloadNewestSession();
            return null;
#else
            AnalyticsWriter.FlushNow();
            Directory.CreateDirectory(Folder);
            string zip = Path.Combine(Application.persistentDataPath,
                "pimlr-analytics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".zip");
            System.IO.Compression.ZipFile.CreateFromDirectory(Folder, zip);
            Application.OpenURL("file:///" + Path.GetDirectoryName(zip).Replace('\\', '/'));
            return zip;
#endif
        }

        /// <summary>WebGL: saves the newest .jsonl through the browser's download.</summary>
        public static void DownloadNewestSession()
        {
            AnalyticsWriter.FlushNow();
            string newest = AnalyticsWriter.NewestFile();
            if (newest == null) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            AnalyticsWebGL.Download(Path.GetFileName(newest), File.ReadAllText(newest));
#else
            Application.OpenURL("file:///" + Path.GetDirectoryName(newest).Replace('\\', '/'));
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EventLogged = null;
            Artist = "thurz";
            ResetSession();
        }
    }

    /// <summary>Ordered key/value fields for one event's "d" object.</summary>
    public sealed class AnalyticsData
    {
        internal static readonly AnalyticsData Empty = new AnalyticsData();
        internal readonly List<KeyValuePair<string, object>> items = new List<KeyValuePair<string, object>>(8);

        public AnalyticsData Add(string key, object value)
        {
            items.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public static AnalyticsData With(string key, object value) => new AnalyticsData().Add(key, value);
    }

    /// <summary>Tiny allocation-light JSON writer (no package dependency, WebGL-safe).</summary>
    internal static class Json
    {
        public static void Field(StringBuilder sb, string key, object value, bool first = false)
        {
            if (!first) sb.Append(',');
            String(sb, key);
            sb.Append(':');
            Write(sb, value);
        }

        public static void Write(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: String(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case float f: Number(sb, f); return;
                case double d: Number(sb, d); return;
                case Enum e: String(sb, e.ToString()); return;
                case Vector3 p:
                    sb.Append('['); Number(sb, p.x); sb.Append(','); Number(sb, p.y); sb.Append(','); Number(sb, p.z); sb.Append(']');
                    return;
                case AnalyticsData data:
                    sb.Append('{');
                    for (int k = 0; k < data.items.Count; k++)
                        Field(sb, data.items[k].Key, data.items[k].Value, k == 0);
                    sb.Append('}');
                    return;
                case IDictionary<string, int> dict:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in dict) { Field(sb, kv.Key, kv.Value, first); first = false; }
                    sb.Append('}');
                    return;
                }
                case IDictionary<string, float> fdict:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in fdict) { Field(sb, kv.Key, kv.Value, first); first = false; }
                    sb.Append('}');
                    return;
                }
                case IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (var item in list) { if (!first) sb.Append(','); Write(sb, item); first = false; }
                    sb.Append(']');
                    return;
                }
                default: String(sb, v.ToString()); return;
            }
        }

        static void Number(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            sb.Append(Math.Round(d, 3).ToString("0.###", CultureInfo.InvariantCulture));
        }

        static void String(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
