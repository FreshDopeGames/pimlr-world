// PIMLR #UI Slice 5: what happens to the music when the Pause screen opens / closes (Craig, Nov 27).
//   Open   the run's track (MusicSystem) is PARKED: paused at its position, its Track Boost untouched; the base
//          soundtrack (SoundtrackDucker) is paused too. The Music Library player is ready to play.
//   Close  the Library stops; the run's track resumes from where it was (if it was playing) and the soundtrack resumes.
//   Activate Boost (a Library tile): the run's track is NOT resumed; MusicSystem.ChangeMusic cues the chosen track from
//          its start and starts its boost through the normal UI_GoalPanel.StartBoost path.
// The run track is paused with AudioSource.Pause (not MusicSystem.PlayMusic), so no boost is cleared and no boost popup
// plays. MusicSystem's own "ended on its own" check ignores a parked track (it's mid-clip).
//
// Unreal note: the music subsystem's PushPauseContext / PopPauseContext, using UAudioComponent::SetPaused.

using System.Collections.Generic;
using UnityEngine;

public static class PimlrPauseAudio
{
    public static bool Open { get; private set; }
    /// <summary>True while the run's track is parked by the Pause screen (analytics ignores the stop).</summary>
    public static bool RunParked { get; private set; }

    static AudioSource runSource;
    static readonly List<AudioSource> parkedSoundtracks = new List<AudioSource>();

    public static void Enter()
    {
        if (Open) return;
        Open = true;
        var ms = SceneManagerScript.Instance ? SceneManagerScript.Instance.musicSystem : null;
        runSource = ms ? ms.musicSystem : null;
        RunParked = runSource && runSource.isPlaying;
        if (RunParked) runSource.Pause();
        PimlrLibraryPlayer.Instance.MatchOutput(runSource);

        parkedSoundtracks.Clear();
        foreach (var d in Object.FindObjectsByType<SoundtrackDucker>(FindObjectsInactive.Exclude))
            foreach (var s in d.GetComponents<AudioSource>())
                if (s && s.isPlaying) { s.Pause(); parkedSoundtracks.Add(s); }
    }

    /// <param name="resumeRun">False for Activate Boost: the caller starts a new run track instead.</param>
    public static void Exit(bool resumeRun = true)
    {
        if (!Open) return;
        Open = false;
        if (PimlrLibraryPlayer.Instance.Current != null) PimlrLibraryPlayer.Instance.Stop("pause_menu_exit");
        if (RunParked && runSource && resumeRun) runSource.UnPause();
        RunParked = false;
        foreach (var s in parkedSoundtracks) if (s) s.UnPause();
        parkedSoundtracks.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Open = false; RunParked = false; runSource = null; parkedSoundtracks.Clear(); }
}
