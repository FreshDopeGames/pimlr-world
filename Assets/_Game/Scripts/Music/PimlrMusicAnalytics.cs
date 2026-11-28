// PIMLR #ANL #UI Slice 5: music listening analytics (investor KPIs: how much people listen, to what, and where).
// Events (all through PimlrAnalytics, so the usual envelope + consent apply):
//   track_play   {track, title, artist, featured, project, context, trigger}
//   track_stop   {track, title, artist, featured, project, context, listened_s, length_s, fraction, completed, reason}
//   track_purchase (PimlrTrackStore), library_open / library_close (PimlrPauseSession)
//   context  "run"     the gameplay track (MusicSystem): watched here, nothing in MusicSystem changes
//            "library" the Pause screen's Music Library player (PimlrLibraryPlayer logs its own)
//   listened_s counts only time actually playing (paused time excluded), so it can't be padded by leaving a track paused.
//
// Unreal note: the music subsystem broadcasts OnTrackStarted / OnTrackStopped; an analytics listener formats the same events.

using PIMLR.Analytics;
using UnityEngine;

public class PimlrMusicAnalytics : MonoBehaviour
{
    public static AnalyticsData TrackData(MusicData d, AudioClip clip = null)
    {
        clip = clip ? clip : (d != null ? d.audioClip : null);
        var a = new AnalyticsData().Add("track", clip ? clip.name : "");
        if (d != null)
            a.Add("title", PimlrTrackInfo.Title(d)).Add("artist", PimlrTrackInfo.Artist(d))
             .Add("featured", PimlrTrackInfo.Featured(d)).Add("project", PimlrTrackInfo.Project(d));
        return a;
    }

    /// <summary>One listening segment: a clip from start/resume until it ends, changes or is stopped.</summary>
    public class Segment
    {
        public readonly string context;
        public AudioClip clip;
        public MusicData data;
        public float listened;
        public bool open;
        public Segment(string context) { this.context = context; }

        public void Start(AudioClip c, string trigger)
        {
            if (open) Stop("changed", false);
            clip = c; data = PimlrTrackInfo.Find(c); listened = 0f; open = c;
            if (!open) return;
            PimlrAnalytics.Log("track_play", TrackData(data, clip).Add("context", context).Add("trigger", trigger));
        }

        public void Tick(bool playing, float dt) { if (open && playing) listened += dt; }

        public void Stop(string reason, bool completed)
        {
            if (!open) return;
            open = false;
            float len = clip ? clip.length : 0f;
            PimlrAnalytics.Log("track_stop", TrackData(data, clip).Add("context", context)
                .Add("listened_s", Mathf.Round(listened * 10f) / 10f).Add("length_s", Mathf.Round(len))
                .Add("fraction", len > 0f ? Mathf.Round(Mathf.Clamp01(listened / len) * 100f) / 100f : 0f)
                .Add("completed", completed).Add("reason", reason));
        }
    }

    // ------------------------------------------------------------------ run-track watcher (self-creating)

    static PimlrMusicAnalytics instance;
    readonly Segment run = new Segment("run");
    AudioSource src;
    float lastTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance) return;
        var go = new GameObject("[PIMLR_MusicAnalytics]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PimlrMusicAnalytics>();
    }

    void Update()
    {
        var ms = SceneManagerScript.Instance ? SceneManagerScript.Instance.musicSystem : null;
        var s = ms ? ms.musicSystem : null;
        if (s != src) { if (run.open) run.Stop("scene", false); src = s; lastTime = 0f; }
        if (!src) return;

        bool playing = src.isPlaying;
        var clip = src.clip;
        if (playing && clip && (!run.open || run.clip != clip)) run.Start(clip, run.open ? "change" : "play");
        else if (playing && run.open && src.time + 0.5f < lastTime && src.loop) { run.Stop("end", true); run.Start(clip, "repeat"); }
        run.Tick(playing, Time.unscaledDeltaTime);

        // Ended on its own: stopped, at (or reset to) the start / end of the clip.
        if (run.open && !playing && clip && (src.time <= 0.05f || src.time >= clip.length - 0.1f) && run.listened > 0.5f && !PimlrPauseAudio.RunParked)
            run.Stop("end", true);
        lastTime = src.time;
    }

    void OnApplicationQuit() { if (run.open) run.Stop("quit", false); }
}
