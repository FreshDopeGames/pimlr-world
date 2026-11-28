// PIMLR #UI Slice 5: the Music Library's own player (Craig, Nov 27).
// Separate from the run's MusicSystem: the run's track, position and Track Boost are parked while the Pause screen is
// open (PimlrPauseAudio) and come back on Resume; this player is stopped when the Pause screen closes.
// Plays through the CURRENT PROJECT'S UNLOCKED tracks only (so project playback reflects the player's choice), in
// track order or shuffled, with repeat Off / All / One. Default: repeat All, so a player can pause, pick a track and
// walk away. Shuffle / repeat are remembered (PlayerPrefs).
// Volume follows Settings > Background Music. Analytics: track_play / track_stop with context "library".
//
// Unreal note: a second UAudioComponent owned by the music subsystem (2D, not paused with the game), with the same
// queue logic; OnAudioFinished drives auto-advance.

using System;
using System.Collections.Generic;
using UnityEngine;

public class PimlrLibraryPlayer : MonoBehaviour
{
    public enum Repeat { Off = 0, All = 1, One = 2 }

    const string KShuffle = "PIMLR_Lib_Shuffle", KRepeat = "PIMLR_Lib_Repeat";

    static PimlrLibraryPlayer instance;
    public static PimlrLibraryPlayer Instance
    {
        get
        {
            if (instance) return instance;
            var go = new GameObject("[PIMLR_LibraryPlayer]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PimlrLibraryPlayer>();
            return instance;
        }
    }

    /// <summary>Raised when the track, play state, shuffle or repeat changes (UI refresh).</summary>
    public static event Action Changed;

    public AudioSource Source { get; private set; }
    public MusicData Current { get; private set; }
    public string Project { get; private set; }
    public bool IsPlaying => Source && Source.isPlaying;
    public bool Shuffle { get => PlayerPrefs.GetInt(KShuffle, 0) == 1; set { PlayerPrefs.SetInt(KShuffle, value ? 1 : 0); shuffleBag.Clear(); Raise(); } }
    public Repeat RepeatMode { get => (Repeat)PlayerPrefs.GetInt(KRepeat, (int)Repeat.All); set { PlayerPrefs.SetInt(KRepeat, (int)value); Raise(); } }
    public float Time01 => Source && Source.clip && Source.clip.length > 0f ? Mathf.Clamp01(Source.time / Source.clip.length) : 0f;
    public float TimeSeconds => Source ? Source.time : 0f;
    public float Length => Source && Source.clip ? Source.clip.length : 0f;

    readonly PimlrMusicAnalytics.Segment seg = new PimlrMusicAnalytics.Segment("library");
    readonly List<MusicData> shuffleBag = new List<MusicData>();
    bool userPaused;
    float baseVolume = 1f;

    void Awake()
    {
        Source = gameObject.AddComponent<AudioSource>();
        Source.playOnAwake = false; Source.loop = false; Source.spatialBlend = 0f;
        Source.ignoreListenerPause = true;
    }

    /// <summary>Match the run player's mixer routing / level (called when the Pause screen opens).</summary>
    public void MatchOutput(AudioSource like)
    {
        if (!like) return;
        Source.outputAudioMixerGroup = like.outputAudioMixerGroup;
        float mv = Mathf.Max(0.01f, PimlrSettings.MusicVolume);
        baseVolume = Mathf.Clamp01(like.volume / mv); // the run source's own level without the settings factor
        if (baseVolume <= 0.01f) baseVolume = 1f;
    }

    // ------------------------------------------------------------------ queue

    static List<MusicData> Playable(string project)
    {
        var all = PimlrTrackStore.Tracks(project);
        all.RemoveAll(d => !PimlrTrackStore.Owned(d));
        return all;
    }

    public void Play(MusicData d, string trigger = "tap")
    {
        if (d == null || !d.audioClip || !PimlrTrackStore.Owned(d)) return;
        Project = PimlrTrackInfo.Project(d);
        Current = d;
        userPaused = false;
        Source.clip = d.audioClip;
        Source.time = 0f;
        Source.Play();
        seg.Start(d.audioClip, trigger);
        Raise();
    }

    public void TogglePause()
    {
        if (Current == null) { var first = Playable(Project ?? PimlrTrackStore.Projects().Find(_ => true)); if (first.Count > 0) Play(first[0]); return; }
        if (Source.isPlaying) { Source.Pause(); userPaused = true; }
        else { Source.UnPause(); userPaused = false; if (!seg.open) seg.Start(Source.clip, "resume"); }
        Raise();
    }

    public void Next(string trigger = "skip") { var n = Step(+1); if (n != null) { seg.Stop(trigger == "auto" ? "end" : "skip", trigger == "auto"); Play(n, trigger); } }
    public void Previous()
    {
        if (Source.time > 3f) { Source.time = 0f; Raise(); return; } // first press restarts, like most players
        var p = Step(-1); if (p != null) { seg.Stop("skip", false); Play(p, "skip"); }
    }

    public void Seek01(float t) { if (Source.clip) { Source.time = Mathf.Clamp01(t) * Source.clip.length * 0.999f; Raise(); } }

    /// <summary>Stop and forget (Pause screen closed, Activate Boost).</summary>
    public void Stop(string reason)
    {
        seg.Stop(reason, false);
        if (Source) Source.Stop();
        userPaused = false;
        Raise();
    }

    MusicData Step(int dir)
    {
        var list = Playable(Project);
        if (list.Count == 0) return null;
        if (Shuffle && dir > 0)
        {
            if (shuffleBag.Count == 0) { shuffleBag.AddRange(list); shuffleBag.Remove(Current); }
            if (shuffleBag.Count == 0) return Current;
            var pick = shuffleBag[UnityEngine.Random.Range(0, shuffleBag.Count)];
            shuffleBag.Remove(pick);
            return pick;
        }
        int i = Current != null ? list.IndexOf(Current) : -1;
        int j = i + dir;
        if (j >= list.Count || j < 0) j = (j + list.Count) % list.Count;
        return list[j];
    }

    void Update()
    {
        if (!Source) return;
        Source.volume = baseVolume * PimlrSettings.MusicVolume;
        seg.Tick(Source.isPlaying, Time.unscaledDeltaTime);

        // Track finished on its own -> repeat / next / stop.
        if (Current != null && !Source.isPlaying && !userPaused && seg.open && Source.clip && (Source.time <= 0.05f || Source.time >= Source.clip.length - 0.1f))
        {
            if (RepeatMode == Repeat.One) { seg.Stop("end", true); Play(Current, "repeat"); }
            else
            {
                var list = Playable(Project);
                bool last = !Shuffle && list.Count > 0 && list.IndexOf(Current) == list.Count - 1;
                if (RepeatMode == Repeat.Off && (last || (Shuffle && shuffleBag.Count == 0 && list.Count <= 1))) { seg.Stop("end", true); Raise(); }
                else Next("auto");
            }
        }
    }

    static void Raise() => Changed?.Invoke();

    void OnApplicationQuit() => seg.Stop("quit", false);
}
