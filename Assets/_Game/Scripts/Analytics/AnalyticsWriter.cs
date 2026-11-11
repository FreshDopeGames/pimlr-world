using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// PIMLR #ANL: buffers analytics lines and appends them to one .jsonl file per session.
// Flushes every 10 s, every 50 events, on scene change, on pause / focus loss and on quit.
// Retention: files older than 30 days, then oldest-first until the folder is under 50 MB.
// Also owns the session-level events: session_start, session_end, perf (every 30 s), scene_load.
// No scene setup: it creates itself before the first scene loads.
namespace PIMLR.Analytics
{
    [DefaultExecutionOrder(-500)]
    public class AnalyticsWriter : MonoBehaviour
    {
        const float FlushInterval = 10f;
        const int FlushCount = 50;
        const int RetentionDays = 30;
        const long MaxFolderBytes = 50L * 1024 * 1024;
        const float PerfInterval = 30f;

        static AnalyticsWriter instance;
        static readonly List<string> pending = new List<string>(64);
        static string filePath;

        float lastFlush;
        bool ended;

        // perf sampling
        readonly List<float> frameTimes = new List<float>(2048);
        float perfStart;

        // scene load timing
        float sceneUnloadedAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("PIMLR_Analytics");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AnalyticsWriter>();
            go.AddComponent<AnalyticsHooks>();
            if (Debug.isDebugBuild || Application.isEditor)
                go.AddComponent<AnalyticsDebugOverlay>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            pending.Clear();
            filePath = null;
        }

        void Awake()
        {
            PimlrAnalytics.ResetSession();
            string dir = PimlrAnalytics.Folder;
            try
            {
                Directory.CreateDirectory(dir);
                ApplyRetention(dir);
            }
            catch (Exception e) { Debug.LogWarning("PIMLR Analytics: folder setup failed: " + e.Message); }

            filePath = Path.Combine(dir,
                DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "_" + PimlrAnalytics.SessionId + ".jsonl");

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Application.focusChanged += OnFocusChanged;

            lastFlush = Time.realtimeSinceStartup;
            perfStart = Time.realtimeSinceStartup;

            PimlrAnalytics.Log("session_start", new AnalyticsData()
                .Add("res", Screen.width + "x" + Screen.height)
                .Add("quality", QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "")
                .Add("os", SystemInfo.operatingSystem)
                .Add("gpu", SystemInfo.graphicsDeviceName)
                .Add("ram_mb", SystemInfo.systemMemorySize)
                .Add("consent", PimlrAnalytics.Consent));
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Application.focusChanged -= OnFocusChanged;
        }

        public static void Enqueue(string line)
        {
            pending.Add(line);
            if (pending.Count >= FlushCount) FlushNow();
        }

        public static void FlushNow()
        {
            if (pending.Count == 0 || string.IsNullOrEmpty(filePath)) return;
            try
            {
                var sb = new StringBuilder(pending.Count * 200);
                foreach (var l in pending) sb.Append(l).Append('\n');
                File.AppendAllText(filePath, sb.ToString());
                pending.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
                AnalyticsWebGL.SyncFs(); // persist IndexedDB so a closed tab keeps the data
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("PIMLR Analytics: flush failed: " + e.Message);
                if (pending.Count > 2000) pending.RemoveRange(0, pending.Count - 2000); // never grow without limit
            }
            if (instance) instance.lastFlush = Time.realtimeSinceStartup;
        }

        public static string CurrentFile => filePath;

        public static string NewestFile()
        {
            string dir = PimlrAnalytics.Folder;
            if (!Directory.Exists(dir)) return null;
            return new DirectoryInfo(dir).GetFiles("*.jsonl").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName;
        }

        void Update()
        {
            frameTimes.Add(Time.unscaledDeltaTime);
            float now = Time.realtimeSinceStartup;
            if (now - perfStart >= PerfInterval) LogPerf(now);
            if (now - lastFlush >= FlushInterval) FlushNow();
        }

        void LogPerf(float now)
        {
            if (frameTimes.Count > 0)
            {
                float total = 0f;
                foreach (float f in frameTimes) total += f;
                frameTimes.Sort();
                // 1% low = average fps across the slowest 1% of frames
                int n = Mathf.Max(1, frameTimes.Count / 100);
                float slow = 0f;
                for (int i = frameTimes.Count - n; i < frameTimes.Count; i++) slow += frameTimes[i];

                PimlrAnalytics.Log("perf", new AnalyticsData()
                    .Add("fps_avg", total > 0f ? frameTimes.Count / total : 0f)
                    .Add("fps_1pct_low", slow > 0f ? n / slow : 0f)
                    .Add("frames", frameTimes.Count)
                    .Add("mem_mb", GC.GetTotalMemory(false) / (1024f * 1024f))
                    .Add("alloc_mb", UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f)));
            }
            frameTimes.Clear();
            perfStart = now;
        }

        void OnSceneUnloaded(Scene s)
        {
            sceneUnloadedAt = Time.realtimeSinceStartup;
            FlushNow();
        }

        void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            float ms = sceneUnloadedAt >= 0f ? (Time.realtimeSinceStartup - sceneUnloadedAt) * 1000f : Time.realtimeSinceStartup * 1000f;
            PimlrAnalytics.Log("scene_load", new AnalyticsData().Add("name", s.name).Add("load_ms", Mathf.RoundToInt(ms)));
            FlushNow();
        }

        void OnFocusChanged(bool focused)
        {
            if (!focused) FlushNow();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) FlushNow();
        }

        void OnApplicationQuit()
        {
            if (ended) return;
            ended = true;
            LogPerf(Time.realtimeSinceStartup);
            PimlrAnalytics.Log("session_end", AnalyticsData.With("duration", Time.realtimeSinceStartup));
            FlushNow();
        }

        static void ApplyRetention(string dir)
        {
            var files = new DirectoryInfo(dir).GetFiles("*.jsonl").OrderBy(f => f.LastWriteTimeUtc).ToList();
            DateTime cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            foreach (var f in files.Where(f => f.LastWriteTimeUtc < cutoff).ToList())
            {
                f.Delete();
                files.Remove(f);
            }
            long total = files.Sum(f => f.Length);
            while (total > MaxFolderBytes && files.Count > 0)
            {
                total -= files[0].Length;
                files[0].Delete();
                files.RemoveAt(0);
            }
        }
    }

    /// <summary>WebGL bridge to Assets/Plugins/WebGL/PimlrAnalytics.jslib.</summary>
    internal static class AnalyticsWebGL
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void PimlrAnalytics_SyncFs();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void PimlrAnalytics_Download(string name, string text);
        public static void SyncFs() => PimlrAnalytics_SyncFs();
        public static void Download(string name, string text) => PimlrAnalytics_Download(name, text);
#else
        public static void SyncFs() { }
        public static void Download(string name, string text) { }
#endif
    }
}
