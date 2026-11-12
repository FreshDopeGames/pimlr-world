// PIMLR #SET: player settings (look sensitivity, invert Y, controller rumble, background music volume).
// Stored in PlayerPrefs, applied live by PimlrSettingsRuntime (self-creating, survives scene loads). The menu that
// edits them is PimlrSettingsMenu (Pause screen + Living Room).
//
// Unreal note: a USaveGame-backed UGameUserSettings subclass; look sensitivity -> Enhanced Input modifiers
// (Scalar / Negate on the Look action), music volume -> a Sound Class mix, rumble -> a bool checked before
// ClientPlayForceFeedback.

using System;
using System.Collections.Generic;
using JUTPS.CameraSystems;
using UnityEngine;

public static class PimlrSettings
{
    const string KLookX = "PIMLR_Set_LookX", KLookY = "PIMLR_Set_LookY", KInvertY = "PIMLR_Set_InvertY",
                 KRumble = "PIMLR_Set_Rumble", KMusic = "PIMLR_Set_MusicVolume";

    public const float MinLook = 0.2f, MaxLook = 3f;

    /// <summary>Raised whenever any setting changes.</summary>
    public static event Action Changed;

    /// <summary>Left / right look speed, as a multiplier on the camera's own setting (1 = default).</summary>
    public static float LookX { get => PlayerPrefs.GetFloat(KLookX, 1f); set => SetF(KLookX, Mathf.Clamp(value, MinLook, MaxLook)); }
    /// <summary>Up / down look speed multiplier (1 = default).</summary>
    public static float LookY { get => PlayerPrefs.GetFloat(KLookY, 1f); set => SetF(KLookY, Mathf.Clamp(value, MinLook, MaxLook)); }
    public static bool InvertY { get => PlayerPrefs.GetInt(KInvertY, 0) == 1; set => SetI(KInvertY, value ? 1 : 0); }
    public static bool Rumble { get => PlayerPrefs.GetInt(KRumble, 1) == 1; set => SetI(KRumble, value ? 1 : 0); }
    /// <summary>Background music volume 0..1 (catalog tracks, the base soundtrack, the music player).</summary>
    public static float MusicVolume { get => PlayerPrefs.GetFloat(KMusic, 1f); set => SetF(KMusic, Mathf.Clamp01(value)); }

    public static void ResetToDefaults()
    {
        foreach (var k in new[] { KLookX, KLookY, KInvertY, KRumble, KMusic }) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    static void SetF(string k, float v) { PlayerPrefs.SetFloat(k, v); PlayerPrefs.Save(); Changed?.Invoke(); }
    static void SetI(string k, int v) { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); Changed?.Invoke(); }
}

/// <summary>PIMLR #SET: applies PimlrSettings to whatever is in the scene (cameras, music sources). Self-creating.</summary>
public class PimlrSettingsRuntime : MonoBehaviour
{
    static PimlrSettingsRuntime instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance) return;
        var go = new GameObject("[PIMLR_Settings]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PimlrSettingsRuntime>();
        go.AddComponent<PimlrSettingsMenu>();
    }

    // camera -> its own (designer) sensitivity, captured the first time we see it
    readonly Dictionary<JUCameraController, Vector2> cameraBase = new Dictionary<JUCameraController, Vector2>();
    // music source -> (volume the game wants, volume we last wrote)
    readonly Dictionary<AudioSource, Vector2> music = new Dictionary<AudioSource, Vector2>();
    float rescan;
    bool dirty = true;

    void OnEnable() { PimlrSettings.Changed += MarkDirty; UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { PimlrSettings.Changed -= MarkDirty; UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene; }
    void MarkDirty() => dirty = true;
    void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { rescan = 0f; dirty = true; }

    void LateUpdate()
    {
        rescan -= Time.unscaledDeltaTime;
        if (rescan <= 0f) { rescan = 1f; Scan(); dirty = true; }
        ApplyMusic(); // every frame: picks up volume changes made by the game (ducking, the music player's slider)
        if (dirty) { dirty = false; ApplyCameras(); }
    }

    void Scan()
    {
        foreach (var cam in FindObjectsByType<JUCameraController>(FindObjectsInactive.Include))
            if (!cameraBase.ContainsKey(cam))
                cameraBase[cam] = new Vector2(cam.GeneralSensibility, Mathf.Abs(cam.GeneralVerticalSensibility));

        // Music = the catalog players (MusicSystem), the base soundtrack (SoundtrackDucker), the Simple Music Player
        // and the PersistentAudioManager. Gameplay sound effects are never touched.
        foreach (var ms in FindObjectsByType<MusicSystem>(FindObjectsInactive.Include)) Track(ms.musicSystem);
        foreach (var d in FindObjectsByType<SoundtrackDucker>(FindObjectsInactive.Include)) foreach (var s in d.GetComponents<AudioSource>()) Track(s);
        foreach (var p in FindObjectsByType<MusicPlayer>(FindObjectsInactive.Include)) foreach (var s in p.GetComponents<AudioSource>()) Track(s);
        if (PersistentAudioManager.Instance) foreach (var s in PersistentAudioManager.Instance.GetComponents<AudioSource>()) Track(s);

        // forget destroyed objects
        var deadCams = new List<JUCameraController>();
        foreach (var k in cameraBase.Keys) if (!k) deadCams.Add(k);
        foreach (var k in deadCams) cameraBase.Remove(k);
        var deadSrc = new List<AudioSource>();
        foreach (var k in music.Keys) if (!k) deadSrc.Add(k);
        foreach (var k in deadSrc) music.Remove(k);
    }

    void Track(AudioSource s)
    {
        if (s && !music.ContainsKey(s)) music[s] = new Vector2(s.volume, float.NaN);
    }

    void ApplyCameras()
    {
        float x = PimlrSettings.LookX, y = PimlrSettings.LookY * (PimlrSettings.InvertY ? -1f : 1f);
        foreach (var kv in cameraBase)
        {
            if (!kv.Key) continue;
            kv.Key.GeneralSensibility = kv.Value.x * x;
            kv.Key.GeneralVerticalSensibility = kv.Value.y * y; // negative = inverted (JU subtracts the vertical axis)
        }
    }

    readonly List<AudioSource> keys = new List<AudioSource>();
    void ApplyMusic()
    {
        float mv = PimlrSettings.MusicVolume;
        keys.Clear(); keys.AddRange(music.Keys);
        foreach (var s in keys)
        {
            if (!s) continue;
            Vector2 v = music[s];
            // Someone else changed the volume since we last wrote it: that's the game's new intended level.
            if (float.IsNaN(v.y) || !Mathf.Approximately(s.volume, v.y)) v.x = s.volume;
            float target = v.x * mv;
            if (!Mathf.Approximately(s.volume, target)) s.volume = target;
            v.y = s.volume;
            music[s] = v;
        }
    }
}
