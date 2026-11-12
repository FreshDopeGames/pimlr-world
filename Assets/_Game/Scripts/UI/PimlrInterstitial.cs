// PIMLR: one place that knows whether a full-screen interstitial is on screen — the loading screen (and its optional
// FMV), the "WASHED" death sequence, a zone card ("Zone Panel" after a fade), or any scripted fade-out / fade-in
// (Save The Fans restart). Enemy arrivals wait until it's clear, so the portal sounds never play behind a loading
// screen and every interstitial gets to play all the way through before enemies show up.
//
//   using (PimlrInterstitial.Hold()) { ... }        // or Begin()/End() around a coroutine's interstitial part
//   yield return PimlrInterstitial.WaitUntilClear();
//
// Unreal note: a UGameInstanceSubsystem with a hold counter + IsLoading from the loading-screen module; spawners
// query it (or bind to an OnInterstitialFinished delegate) before spawning.

using System;
using System.Collections;
using UnityEngine;

public static class PimlrInterstitial
{
    static int holds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        holds = 0;
        // A coroutine holding an interstitial dies with its scene without running its finally block; a new scene
        // must never inherit that hold (it would block every enemy arrival).
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) holds = 0;
    }

    /// <summary>True while any interstitial is on screen (or a scene is loading).</summary>
    public static bool Playing =>
        holds > 0
        || (SceneManagerScript.Instance != null && SceneManagerScript.Instance.IsLoading)
        || PimlrDeathSequence.IsPlaying;

    public static void Begin() => holds++;
    public static void End() => holds = Mathf.Max(0, holds - 1);

    /// <summary>using (PimlrInterstitial.Hold()) { ... } — the hold is released even if the code throws.</summary>
    public static IDisposable Hold() { Begin(); return new Releaser(); }

    sealed class Releaser : IDisposable
    {
        bool done;
        public void Dispose() { if (done) return; done = true; End(); }
    }

    /// <summary>Waits until no interstitial is playing, then a short beat (settle) so nothing starts on the very frame
    /// the screen clears.</summary>
    public static IEnumerator WaitUntilClear(float settle = 0.25f)
    {
        while (Playing) yield return null;
        if (settle > 0f) yield return new WaitForSeconds(settle);
        while (Playing) yield return null;
    }
}
