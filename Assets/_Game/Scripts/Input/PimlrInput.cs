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
    public static bool IsPlayStation => Gamepad.current is DualShockGamepad;

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
    public static void MarkPlayerInVehicle() => inVehicleFrame = Time.frameCount;
    public static bool PlayerInVehicle => Time.frameCount - inVehicleFrame <= 1;

    public static bool VehicleExitDown => Pad != null && Pad.buttonEast.wasPressedThisFrame;     // B / Circle leaves the vehicle

    // The B press that exits a vehicle must not also crouch the character on the same frame.
    static int lastVehicleExitFrame = -10;
    public static void MarkVehicleExit() => lastVehicleExitFrame = Time.frameCount;
    public static bool VehicleExitedRecently => Time.frameCount - lastVehicleExitFrame <= 2;

    const float TriggerDeadzone = 0.08f;
    const float BrakeBeforeReverseSpeed = 1.0f; // m/s: above this, the opposite trigger brakes instead of reversing

    /// <summary>
    /// Racing-game trigger driving: RT = accelerate, LT = brake, then reverse once (nearly) stopped.
    /// Returns the forward/back engine input and whether the brakes should be on.
    /// </summary>
    public static void GamepadVehicleInputs(Rigidbody rb, Transform vehicle, out float vertical, out bool brake)
    {
        vertical = 0f; brake = false;
        if (Pad == null) return;

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
