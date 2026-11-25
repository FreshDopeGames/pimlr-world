using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// PIMLR (online leaderboards, Nov 2026): client for the PIMLR leaderboard server (Tools/LeaderboardServer).
//   - Name entry registers (or, with a PIN, claims) the player's name and stores a per-device key in PlayerPrefs.
//   - RunStats.Began / ClockRestarted are mirrored to the server so it times every run independently.
//   - LeaderboardManager.TrySubmit queues finished runs here; the queue persists in PlayerPrefs and retries until
//     the server answers, so a dropped connection at the playtest never loses a score.
// Auto-created at startup (no scene wiring). Local leaderboards keep working when offline.
//
// Unreal translation: a UGameInstanceSubsystem using FHttpModule (or VaRest in Blueprint) against the same
// endpoints; PlayerPrefs -> USaveGame; the queue -> a TArray in the save game.
public class PimlrOnline : MonoBehaviour
{
    public const string ProductionUrl = "https://pimlrworld.com";

    private const string KeyId = "PIMLR_Online_PlayerId";
    private const string KeySecret = "PIMLR_Online_Secret";
    private const string KeyName = "PIMLR_Online_Name";
    private const string KeyQueue = "PIMLR_Online_Queue_v1";
    private const string KeyPendingReg = "PIMLR_Online_PendingRegistration";

    private static PimlrOnline _instance;
    public static PimlrOnline Instance { get { Ensure(); return _instance; } }

    public static string PlayerId => PlayerPrefs.GetString(KeyId, "");
    private static string Secret => PlayerPrefs.GetString(KeySecret, "");
    public static bool HasAccount => !string.IsNullOrEmpty(PlayerId) && !string.IsNullOrEmpty(Secret);

    /// <summary>Raised after the server accepts a run; carries the player's new ranks.</summary>
    public static event Action<RunResult> RunSubmitted;
    public static RunResult LastRunResult { get; private set; }

    /// <summary>Same origin when running on pimlrworld.com; the production server everywhere else (Editor, itch.io).</summary>
    public static string BaseUrl
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string url = Application.absoluteURL ?? "";
            if (url.StartsWith("https://pimlrworld.com") || url.StartsWith("https://www.pimlrworld.com"))
                return url.Substring(0, url.IndexOf('/', 8) < 0 ? url.Length : url.IndexOf('/', 8));
#endif
            return ProductionUrl;
        }
    }

    private string currentRunId;
    private RunMode currentRunMode;
    private bool flushing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() => Ensure();

    private static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("PimlrOnline");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PimlrOnline>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        RunStats.Began += OnRunBegan;
        RunStats.ClockRestarted += OnClockRestarted;
        PlayerSession.NameChanged += OnNameChanged;
        StartCoroutine(BackgroundLoop());
    }

    private void OnDestroy()
    {
        if (_instance != this) return;
        RunStats.Began -= OnRunBegan;
        RunStats.ClockRestarted -= OnClockRestarted;
        PlayerSession.NameChanged -= OnNameChanged;
    }

    // ------------------------------------------------------------------ identity

    /// <summary>Called by the name-entry screen. Registers the name; if it's taken and a PIN was given, claims it.
    /// When the server can't be reached the name is accepted locally and registered later.</summary>
    public static void Register(string name, string handle, string pin, Action<RegisterResult> done)
    {
        Instance.StartCoroutine(Instance.RegisterRoutine(name, handle, pin, done));
    }

    private IEnumerator RegisterRoutine(string name, string handle, string pin, Action<RegisterResult> done)
    {
        var body = new RegisterRequest { name = name, handle = handle ?? "", pin = pin ?? "" };
        Response r = null;
        yield return Send("POST", "/api/player/register", JsonUtility.ToJson(body), x => r = x);

        if (r.ok) { SaveAccount(r.Parse<AccountResponse>()); done?.Invoke(RegisterResult.Success()); yield break; }

        if (r.offline)
        {
            PlayerPrefs.SetString(KeyPendingReg, JsonUtility.ToJson(body));
            ClearAccount();
            PlayerPrefs.Save();
            Debug.Log("PimlrOnline: server unreachable at name entry; name kept locally and will register when back online.");
            done?.Invoke(RegisterResult.Offline());
            yield break;
        }

        var err = r.Parse<ErrorResponse>() ?? new ErrorResponse();
        if (r.status == 409 && err.error == "name_taken")
        {
            if (string.IsNullOrEmpty(pin))
            {
                done?.Invoke(RegisterResult.Fail(err.hasPin
                    ? "That name is taken. Enter its 4-digit PIN to use it here, or pick another name."
                    : "That name is taken. Pick another name."));
                yield break;
            }
            Response c = null;
            yield return Send("POST", "/api/player/claim", JsonUtility.ToJson(new ClaimRequest { name = name, pin = pin }), x => c = x);
            if (c.ok)
            {
                SaveAccount(c.Parse<AccountResponse>());
                if (!string.IsNullOrEmpty(handle))
                    yield return Send("POST", "/api/player/update", JsonUtility.ToJson(new UpdateRequest { playerId = PlayerId, secret = Secret, handle = handle }), _ => { });
                done?.Invoke(RegisterResult.Success());
            }
            else done?.Invoke(RegisterResult.Fail(c.offline ? "Can't reach the server. Try again." : (c.Parse<ErrorResponse>()?.message ?? "Name or PIN is wrong.")));
            yield break;
        }
        done?.Invoke(RegisterResult.Fail(string.IsNullOrEmpty(err.message) ? "Couldn't save that name. Try another." : err.message));
    }

    private static void SaveAccount(AccountResponse a)
    {
        if (a == null || string.IsNullOrEmpty(a.playerId)) return;
        PlayerPrefs.SetString(KeyId, a.playerId);
        PlayerPrefs.SetString(KeySecret, a.secret);
        PlayerPrefs.SetString(KeyName, a.name);
        PlayerPrefs.DeleteKey(KeyPendingReg);
        PlayerPrefs.Save();
        Debug.Log($"PimlrOnline: signed in as {a.name}.");
    }

    private static void ClearAccount()
    {
        PlayerPrefs.DeleteKey(KeyId);
        PlayerPrefs.DeleteKey(KeySecret);
        PlayerPrefs.DeleteKey(KeyName);
    }

    // "Not you? New session" -> PlayerSession.Reset() raises NameChanged("").
    private void OnNameChanged(string newName)
    {
        if (!string.IsNullOrEmpty(newName)) return;
        ClearAccount();
        PlayerPrefs.DeleteKey(KeyPendingReg);
        PlayerPrefs.Save();
        currentRunId = null;
    }

    // ------------------------------------------------------------------ runs

    private void OnRunBegan(RunSnapshot s)
    {
        currentRunId = null;
        currentRunMode = s.mode;
        if (!HasAccount || (s.mode != RunMode.Story && s.mode != RunMode.Infinite)) return;
        RunMode startedMode = s.mode;
        var req = new RunStartRequest { playerId = PlayerId, secret = Secret, mode = ModeString(s.mode) };
        StartCoroutine(Send("POST", "/api/run/start", JsonUtility.ToJson(req), r =>
        {
            // Ignore a late reply if that run already ended or another one started.
            if (r.ok && RunStats.Active && RunStats.Mode == startedMode)
                currentRunId = r.Parse<RunStartResponse>()?.runId;
        }));
    }

    private void OnClockRestarted()
    {
        if (string.IsNullOrEmpty(currentRunId) || !HasAccount) return;
        StartCoroutine(Send("POST", "/api/run/clock",
            JsonUtility.ToJson(new RunClockRequest { playerId = PlayerId, secret = Secret, runId = currentRunId }), _ => { }));
    }

    /// <summary>Called by LeaderboardManager.TrySubmit for every run it records locally.</summary>
    public static void SubmitRun(RunSnapshot s) => Instance.Enqueue(s);

    private void Enqueue(RunSnapshot s)
    {
        var item = new QueuedRun
        {
            runId = s.mode == currentRunMode ? (currentRunId ?? "") : "",
            mode = ModeString(s.mode),
            elapsed = s.elapsed,
            wave = s.wave,
            kills = s.kills,
            bossKills = s.bossKills,
            coins = s.coinsEarned,
            startZone = s.startZone.ToString(),
            endReason = s.endReason.ToString(),
            build = Application.version,
            anonymousId = PlayerProfile.AnonymousId,
            playerId = PlayerId,
            secret = Secret,
        };
        currentRunId = null;
        var q = LoadQueue();
        q.items.Add(item);
        if (q.items.Count > 50) q.items.RemoveAt(0);
        SaveQueue(q);
        StartCoroutine(Flush());
    }

    private IEnumerator Flush()
    {
        if (flushing) yield break;
        flushing = true;
        try
        {
            var q = LoadQueue();
            while (q.items.Count > 0)
            {
                QueuedRun item = q.items[0];
                if (string.IsNullOrEmpty(item.playerId))
                {
                    // Recorded before the account existed (offline name entry): attach it if this is still the same player.
                    if (HasAccount && item.anonymousId == PlayerProfile.AnonymousId) { item.playerId = PlayerId; item.secret = Secret; }
                    else if (item.anonymousId != PlayerProfile.AnonymousId) { q.items.RemoveAt(0); SaveQueue(q); continue; }
                    else yield break; // still waiting for registration
                }

                Response r = null;
                yield return Send("POST", "/api/run/finish", JsonUtility.ToJson(item), x => r = x);
                if (r.offline || r.status >= 500 || r.status == 429) yield break; // try again later

                q.items.RemoveAt(0);
                SaveQueue(q);
                if (r.ok)
                {
                    LastRunResult = r.Parse<RunResult>();
                    Debug.Log($"PimlrOnline: {item.mode} run submitted.");
                    RunSubmitted?.Invoke(LastRunResult);
                }
                else Debug.LogWarning($"PimlrOnline: run not accepted ({r.status}): {r.text}");
            }
        }
        finally { flushing = false; }
    }

    private IEnumerator BackgroundLoop()
    {
        var wait = new WaitForSecondsRealtime(45f);
        while (true)
        {
            if (!HasAccount && PlayerPrefs.HasKey(KeyPendingReg))
            {
                var pending = JsonUtility.FromJson<RegisterRequest>(PlayerPrefs.GetString(KeyPendingReg));
                RegisterResult result = null;
                yield return RegisterRoutine(pending.name, pending.handle, pending.pin, x => result = x);
                if (result != null && !result.ok)
                {
                    // e.g. someone else took the name while this device was offline: stay local-only for this name.
                    Debug.LogWarning("PimlrOnline: delayed registration failed: " + result.message);
                    PlayerPrefs.DeleteKey(KeyPendingReg);
                }
            }
            yield return Flush();
            yield return wait;
        }
    }

    // ------------------------------------------------------------------ boards

    public static void GetBoard(string cat, string period, int limit, Action<BoardResponse> done)
    {
        string path = $"/api/leaderboard?cat={cat}&period={period}&limit={limit}&playerId={UnityWebRequest.EscapeURL(PlayerId)}";
        Instance.StartCoroutine(Instance.Send("GET", path, null, r => done?.Invoke(r.ok ? r.Parse<BoardResponse>() : null)));
    }

    // ------------------------------------------------------------------ http

    private IEnumerator Send(string method, string path, string json, Action<Response> done)
    {
        using (var req = new UnityWebRequest(BaseUrl + path, method))
        {
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 15;
            yield return req.SendWebRequest();
            var r = new Response
            {
                status = req.responseCode,
                text = req.downloadHandler != null ? req.downloadHandler.text : "",
                // No server answer, or the API isn't installed / is restarting behind nginx: treat as offline (queue + retry).
                offline = req.result == UnityWebRequest.Result.ConnectionError || req.responseCode == 0
                          || req.responseCode == 404 || req.responseCode == 502 || req.responseCode == 503 || req.responseCode == 504,
            };
            r.ok = req.result == UnityWebRequest.Result.Success && r.status >= 200 && r.status < 300;
            done?.Invoke(r);
        }
    }

    private static string ModeString(RunMode m) => m == RunMode.Story ? "story" : "infinite";

    private static RunQueue LoadQueue()
    {
        try { return JsonUtility.FromJson<RunQueue>(PlayerPrefs.GetString(KeyQueue, "")) ?? new RunQueue(); }
        catch { return new RunQueue(); }
    }

    private static void SaveQueue(RunQueue q)
    {
        PlayerPrefs.SetString(KeyQueue, JsonUtility.ToJson(q));
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------ DTOs (field names match the server JSON)

    private class Response
    {
        public long status; public string text; public bool ok; public bool offline;
        public T Parse<T>() where T : class
        {
            if (string.IsNullOrEmpty(text)) return null;
            try { return JsonUtility.FromJson<T>(text); } catch { return null; }
        }
    }

    [Serializable] private class RegisterRequest { public string name; public string handle; public string pin; }
    [Serializable] private class ClaimRequest { public string name; public string pin; }
    [Serializable] private class UpdateRequest { public string playerId; public string secret; public string handle; }
    [Serializable] private class AccountResponse { public string playerId; public string secret; public string name; }
    [Serializable] private class ErrorResponse { public string error; public string message; public bool hasPin; }
    [Serializable] private class RunStartRequest { public string playerId; public string secret; public string mode; }
    [Serializable] private class RunStartResponse { public string runId; }
    [Serializable] private class RunClockRequest { public string playerId; public string secret; public string runId; }
    [Serializable] private class RunQueue { public List<QueuedRun> items = new List<QueuedRun>(); }

    [Serializable]
    private class QueuedRun
    {
        public string playerId, secret, runId, mode, startZone, endReason, build, anonymousId;
        public float elapsed;
        public int wave, kills, bossKills, coins;
    }

    public class RegisterResult
    {
        public bool ok; public bool offline; public string message;
        public static RegisterResult Success() => new RegisterResult { ok = true };
        public static RegisterResult Offline() => new RegisterResult { ok = true, offline = true };
        public static RegisterResult Fail(string msg) => new RegisterResult { ok = false, message = msg };
    }

    [Serializable] public class Rank { public int rank; public int of; }
    [Serializable] public class RankSet { public Rank today; public Rank week; public Rank all; }
    [Serializable] public class Ranks { public RankSet story_time; public RankSet inf_wave; public RankSet inf_kills; public RankSet inf_survival; }
    [Serializable] public class RunResult { public bool ok; public string runId; public Ranks ranks; }

    [Serializable] public class BoardRow { public int rank; public string name; public float value; public int wave; public int kills; public float survival; public bool you; }
    [Serializable] public class BoardYou { public int rank; public string name; public float value; }
    [Serializable]
    public class BoardResponse
    {
        public string cat; public string label; public string unit; public string period; public int total;
        public List<BoardRow> rows = new List<BoardRow>();
        public BoardYou you; public string today; public string weekStart; public string weekEndsPT;
    }
}
