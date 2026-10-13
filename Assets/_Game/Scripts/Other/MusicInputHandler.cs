// PIMLR #8: Gamepad + track controls handler.
// Reads the Next / Previous / PlayPause inputs and drives the existing MusicSystem
// (ChangeMusicForward / ChangeMusicBackward / PlayMusic). It does NOT modify MusicSystem.
//
// WHY this reads Gamepad.current directly instead of the JUTPS InputAction wrapper:
//   The generated wrapper "JUTPSInputControlls.cs" embeds the input map as a baked JSON
//   string (InputActionAsset.FromJson(...)). That .cs is NOT auto-regenerated without the
//   Unity Editor, so the new "PlayPause" action and the leftShoulder/rightShoulder/start
//   bindings added to JUTPSInputControlls.inputactions are NOT visible to the wrapper at
//   runtime until someone reopens the .inputactions asset in the Editor and lets Unity
//   regenerate the .cs. Reading the device directly guarantees these controls work right
//   now, and matches the bindings declared in the .inputactions (LB=Previous, RB=Next,
//   Start=PlayPause). Keyboard fallbacks mirror the existing map (Q=Previous, E=Next).
//
// EDITOR STEP (residual): drop this component on a GameObject that lives in the gameplay
//   scenes (SceneStaticEU / YannicksWorld) -- e.g. on the SceneManager / GameExecutionManager
//   object, or any persistent manager. Optionally assign the MusicSystem reference; if left
//   null it auto-resolves via SceneManagerScript.Instance.musicSystem at runtime.
//   ALSO (optional): reopen Assets/Julhiecio TPS Controller/Inputs/JUTPSInputControlls.inputactions
//   in the Editor so Unity regenerates JUTPSInputControlls.cs with the new PlayPause action
//   and shoulder bindings (only needed if other systems consume them through the wrapper).

using UnityEngine;
using UnityEngine.InputSystem;

public class MusicInputHandler : MonoBehaviour
{
    [Header("Music System")]
    [Tooltip("Optional. If left empty, resolves at runtime via SceneManagerScript.Instance.musicSystem.")]
    [SerializeField] private MusicSystem musicSystem; // PIMLR #8: scene ref hook (TODO: assign in Editor, or leave null for auto-resolve).

    [Header("Behaviour")]
    [Tooltip("If true, also reads keyboard Q/E (Previous/Next) and Space-on-nothing-else mappings for desktop testing.")]
    [SerializeField] private bool enableKeyboardFallback = true;

    private MusicSystem Music
    {
        get
        {
            if (musicSystem != null) return musicSystem;
            // PIMLR #8: MusicSystem has no singleton; the live instance registers itself on
            // SceneManagerScript.Instance.musicSystem (see MusicSystem.Start()).
            if (SceneManagerScript.Instance != null)
                musicSystem = SceneManagerScript.Instance.musicSystem;
            return musicSystem;
        }
    }

    private void Update()
    {
        bool next = false;
        bool previous = false;
        bool playPause = false;

        // --- Gamepad (matches JUTPSInputControlls.inputactions: LB=Previous, RB=Next, Start=PlayPause) ---
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.rightShoulder.wasPressedThisFrame) next = true;
            if (pad.leftShoulder.wasPressedThisFrame) previous = true;
            // PIMLR: play/pause moved from Start to Y / Triangle; Start / Options is the menu/pause button.
            if (pad.buttonNorth.wasPressedThisFrame) playPause = true;
        }

        // --- Keyboard fallback for desktop testing (mirrors existing map Q=Previous, E=Next) ---
        if (enableKeyboardFallback)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.eKey.wasPressedThisFrame) next = true;
                if (kb.qKey.wasPressedThisFrame) previous = true;
                // PIMLR #8: no existing keyboard PlayPause binding; expose P for parity/testing only.
                if (kb.pKey.wasPressedThisFrame) playPause = true;
            }
        }

        if (next) DoNext();
        if (previous) DoPrevious();
        if (playPause) DoPlayPause();
    }

    private void DoNext()
    {
        MusicSystem m = Music;
        if (m == null) return;
        m.ChangeMusicForward(); // PIMLR #8: advance to next track.
    }

    private void DoPrevious()
    {
        MusicSystem m = Music;
        if (m == null) return;
        m.ChangeMusicBackward(); // PIMLR #8: go to previous track.
    }

    private void DoPlayPause()
    {
        MusicSystem m = Music;
        if (m == null) return;
        m.PlayMusic(); // PIMLR #8: PlayMusic() toggles play/pause (see MusicSystem.PlayMusic()).
    }
}
