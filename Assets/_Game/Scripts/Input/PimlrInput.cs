using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

public enum PimlrControlScheme { KeyboardMouse, Xbox, PlayStation }

/// <summary>
/// PIMLR: one source of truth for "which controls is the player using right now", plus button labels and the
/// gamepad-only buttons that JUTPS doesn't own (music, stance cycle). Scheme = last device actually used;
/// starts on gamepad when one is connected.
/// </summary>
public static class PimlrInput
{
    public static bool UsingGamepad { get; private set; }
    public static bool IsPlayStation
    {
        get
        {
            var pad = Gamepad.current;
            if (pad == null) return false;
            if (pad is DualShockGamepad) return true;           // native (Editor / standalone)

            if (pad.deviceId != cachedPadId)                     // WebGL: identify by name, cached per device
            {
                cachedPadId = pad.deviceId;
                cachedIsPs = LooksLikePlayStation(pad);
            }
            return cachedIsPs;
        }
    }

    static int cachedPadId = -1;
    static bool cachedIsPs;

    static bool LooksLikePlayStation(Gamepad pad)
    {
        var d = pad.description;
        string s = string.Join(" ", pad.displayName, pad.name, pad.layout, d.product, d.manufacturer, d.interfaceName)
                        .ToLowerInvariant();
        return s.Contains("dualsense") || s.Contains("dualshock") || s.Contains("playstation") ||
            s.Contains("wireless controller") || s.Contains("054c") || s.Contains("sony");
    }

    public static PimlrControlScheme Scheme =>
        !UsingGamepad ? PimlrControlScheme.KeyboardMouse : (IsPlayStation ? PimlrControlScheme.PlayStation : PimlrControlScheme.Xbox);

    /// <summary>Raised when the active scheme changes (keyboard <-> gamepad, or Xbox <-> PlayStation pad).</summary>
    public static event Action SchemeChanged;

    /// <summary>True while a vehicle "to Ride" prompt is showing, so gamepad A enters instead of also jumping.</summary>
    public static bool InteractPromptVisible => Time.frameCount - lastPromptFrame <= 1;
    static int lastPromptFrame = -10;
    public static void MarkInteractPromptVisible() => lastPromptFrame = Time.frameCount;

    static PimlrControlScheme lastScheme;

    internal static void SetUsingGamepad(bool value, bool force = false)
    {
        UsingGamepad = value;
        var scheme = Scheme;
        if (!force && scheme == lastScheme) return;
        lastScheme = scheme;
        SchemeChanged?.Invoke();
    }

    // ---- gamepad buttons handled outside JUTPS (pressed this frame) ----
    static Gamepad Pad => Gamepad.current;
    public static bool StanceCycleDown => Pad != null && Pad.buttonEast.wasPressedThisFrame;     // B / Circle
    public static bool MenuButtonDown => Pad != null && Pad.startButton.wasPressedThisFrame;      // Start / Options
    // Music (Y play/pause, LB/RB tracks) is handled by MusicInputHandler in the gameplay scenes.
    // Set every frame by the player's DriveVehicles: true while in a vehicle (driving or drive-by seat).
    static int inVehicleFrame = -10;
    static bool inVehicleIsBike;
    public static void MarkPlayerInVehicle(bool isBike = false) { inVehicleFrame = Time.frameCount; inVehicleIsBike = isBike; }
    public static bool PlayerInVehicle => Time.frameCount - inVehicleFrame <= 1;
    /// <summary>PIMLR (post-playtest): true while the player rides the bike (not the car).</summary>
    public static bool PlayerOnBike => PlayerInVehicle && inVehicleIsBike;

    public static bool VehicleExitDown => Pad != null && Pad.buttonEast.wasPressedThisFrame;     // B / Circle leaves the vehicle

    // The B press that exits a vehicle must not also crouch the character on the same frame.
    static int lastVehicleExitFrame = -10;
    public static void MarkVehicleExit()
    {
        lastVehicleExitFrame = Time.frameCount;
        BlockGameplayInputFor(VehicleExitButtonPause, blockMovement: false); // still tapping A to pedal -> no jump on landing
    }

    // ---- PIMLR (post-playtest): short gameplay input pause ----
    // After a dialogue closes, buttons AND movement wait this long (real seconds) so the press that confirmed the
    // last line can't also jump / fire / walk off. Camera look is never blocked. JUInputManager applies it.
    public static float DialogueInputPause = 0.5f;
    public static float UnpauseButtonPause = 0.4f;     // Resume pressed with A on the pause menu
    public static float VehicleExitButtonPause = 0.5f;

    static float buttonsBlockedUntil = -1f, movementBlockedUntil = -1f;

    /// <summary>Ignore gameplay buttons (and movement, if asked) for the next <paramref name="seconds"/> real seconds.</summary>
    public static void BlockGameplayInputFor(float seconds, bool blockMovement = true)
    {
        float until = Time.realtimeSinceStartup + seconds;
        buttonsBlockedUntil = Mathf.Max(buttonsBlockedUntil, until);
        if (blockMovement) movementBlockedUntil = Mathf.Max(movementBlockedUntil, until);
    }

    /// <summary>Call when a dialogue window closes and the player gets control back.</summary>
    public static void OnDialogueClosed() => BlockGameplayInputFor(DialogueInputPause);

    public static bool GameplayButtonsBlocked => Time.realtimeSinceStartup < buttonsBlockedUntil;
    public static bool GameplayMovementBlocked => Time.realtimeSinceStartup < movementBlockedUntil;
    public static bool VehicleExitedRecently => Time.frameCount - lastVehicleExitFrame <= 2;

    const float TriggerDeadzone = 0.08f;
    const float BrakeBeforeReverseSpeed = 1.0f; // m/s: above this, the opposite trigger brakes instead of reversing

    // ---- PIMLR (post-playtest): bike pedalling on a controller ----
    // TapToPedal (default): A / Cross pedals - every tap is a pedal stroke, tapping fast = full speed; holding it
    //   cruises at a lower speed. Left stick steers, pull it back to brake then reverse. RT / LT stay Fire / Aim,
    //   so players can shoot while riding.
    // Triggers: the playtest scheme (RT accelerate, LT brake / reverse, no shooting while riding).
    public static PimlrPedalMode PedalMode = PimlrPedalMode.TapToPedal;
    /// <summary>True when RT / LT keep their on-foot Fire / Aim roles while the player rides.</summary>
    // PIMLR (post-playtest): bike only. The car is driving-only: RT accelerates, no shooting.
    public static bool CanShootWhileRiding => PedalMode == PimlrPedalMode.TapToPedal && PlayerOnBike;

    public static float PedalStroke = 0.3f;       // power added per tap (4 taps/sec holds full speed against decay)
    public static float PedalDecayPerSec = 0.9f;  // power lost per second when not tapping
    public static float PedalHoldCruise = 0.5f;   // holding A settles here: accessible, but slower than tapping
    public static float PedalHoldRampPerSec = 1.0f;   // PIMLR: holding A reaches cruise (0.5) in 0.5 s
    const float StickBrakeThreshold = -0.5f;      // left stick Y below this = brake / reverse

    /// <summary>0..1 current pedal power (for a cadence meter on the HUD if wanted).</summary>
    public static float PedalPower { get; private set; }
    static int pedalFrame = -1;

    static void UpdatePedal()
    {
        if (pedalFrame == Time.frameCount) return; // several vehicles may ask in one frame
        if (Time.frameCount - pedalFrame > 2) PedalPower = 0f; // fresh ride: no leftover power from the last one
        pedalFrame = Time.frameCount;

        var a = Pad.buttonSouth;
        float dt = Time.deltaTime;
        if (a.wasPressedThisFrame) PedalPower = Mathf.Min(1f, PedalPower + PedalStroke);

        if (a.isPressed && PedalPower < PedalHoldCruise)
            PedalPower = Mathf.MoveTowards(PedalPower, PedalHoldCruise, PedalHoldRampPerSec * dt);
        else
            PedalPower = Mathf.Max(a.isPressed ? PedalHoldCruise : 0f, PedalPower - PedalDecayPerSec * dt);
    }

    /// <summary>
    /// Gamepad vehicle input. TapToPedal: A taps pedal, left stick back brakes / reverses.
    /// Triggers: RT = accelerate, LT = brake, then reverse once (nearly) stopped.
    /// Returns the forward/back engine input and whether the brakes should be on.
    /// </summary>
    public static void GamepadVehicleInputs(Rigidbody rb, Transform vehicle, out float vertical, out bool brake, bool pedalVehicle = true)
    {
        vertical = 0f; brake = false;
        if (Pad == null) return;

        // pedalVehicle = false (the car): always RT accelerate / LT brake-reverse.
        if (pedalVehicle && PedalMode == PimlrPedalMode.TapToPedal)
        {
            UpdatePedal();
            float speed = rb ? Vector3.Dot(rb.velocity, vehicle.forward) : 0f;
            float stickY = Pad.leftStick.ReadValue().y;

            if (stickY < StickBrakeThreshold)
            {
                PedalPower = 0f;
                if (speed > BrakeBeforeReverseSpeed) brake = true;   // still rolling forward: brake first
                else vertical = stickY;                             // stopped: back up
            }
            else if (PedalPower > 0f && speed < -BrakeBeforeReverseSpeed) brake = true; // pedalling while rolling back
            else vertical = PedalPower;
            return;
        }

        float rt = Pad.rightTrigger.ReadValue(); if (rt < TriggerDeadzone) rt = 0f;
        float lt = Pad.leftTrigger.ReadValue();  if (lt < TriggerDeadzone) lt = 0f;
        float forwardSpeed = rb ? Vector3.Dot(rb.velocity, vehicle.forward) : 0f;

        if (lt > 0f && forwardSpeed > BrakeBeforeReverseSpeed)        { brake = true; vertical = 0f; }  // braking while rolling forward
        else if (rt > 0f && forwardSpeed < -BrakeBeforeReverseSpeed)  { brake = true; vertical = 0f; }  // braking while rolling backward
        else vertical = Mathf.Clamp(rt - lt, -1f, 1f);
    }

    // ---- labels for UI / prompts ----
    public static string Label(PimlrButton b)
    {
        switch (Scheme)
        {
            case PimlrControlScheme.PlayStation:
                switch (b)
                {
                    case PimlrButton.Interact: return "X"; // the Cross (✕) button - players call it X
                    case PimlrButton.Jump: return "X"; // the Cross (✕) button - players call it X
                    case PimlrButton.Reload: return "Square";
                    case PimlrButton.Stance: return "Circle";
                    case PimlrButton.PlayTrack: return "Triangle";
                    case PimlrButton.PrevTrack: return "L1";
                    case PimlrButton.NextTrack: return "R1";
                    case PimlrButton.Fire: return "R2";
                    case PimlrButton.Aim: return "L2";
                    case PimlrButton.Run: return "L3";
                }
                break;
            case PimlrControlScheme.Xbox:
                switch (b)
                {
                    case PimlrButton.Interact: return "A";
                    case PimlrButton.Jump: return "A";
                    case PimlrButton.Reload: return "X";
                    case PimlrButton.Stance: return "B";
                    case PimlrButton.PlayTrack: return "Y";
                    case PimlrButton.PrevTrack: return "LB";
                    case PimlrButton.NextTrack: return "RB";
                    case PimlrButton.Fire: return "RT";
                    case PimlrButton.Aim: return "LT";
                    case PimlrButton.Run: return "LS";
                }
                break;
            default:
                switch (b)
                {
                    case PimlrButton.Interact: return "F";
                    case PimlrButton.Jump: return "Space";
                    case PimlrButton.Reload: return "R";
                    case PimlrButton.Stance: return "C / Z";
                    case PimlrButton.PlayTrack: return "";
                    case PimlrButton.PrevTrack: return "";
                    case PimlrButton.NextTrack: return "";
                    case PimlrButton.Fire: return "Left Mouse";
                    case PimlrButton.Aim: return "Right Mouse";
                    case PimlrButton.Run: return "Shift";
                }
                break;
        }
        return "";
    }
}

public enum PimlrButton { Interact, Jump, Reload, Stance, PlayTrack, PrevTrack, NextTrack, Fire, Aim, Run }

public enum PimlrPedalMode { TapToPedal, Triggers }
