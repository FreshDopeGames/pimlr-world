// PIMLR #SET: controller rumble, respecting the Rumble setting. Pulse(low, high, seconds); the strongest active
// pulse wins. Stops when the game is paused (timeScale 0) and on quit.
//
// Unreal note: ClientPlayForceFeedback with a UForceFeedbackEffect asset, gated by the same setting.

using UnityEngine;
using UnityEngine.InputSystem;

public class PimlrRumble : MonoBehaviour
{
    static PimlrRumble instance;
    float low, high, until;
    bool motorsOn;

    static PimlrRumble Instance
    {
        get
        {
            if (!instance)
            {
                var go = new GameObject("[PIMLR_Rumble]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<PimlrRumble>();
            }
            return instance;
        }
    }

    /// <summary>Rumble the current controller. low = heavy motor, high = light motor (0..1).</summary>
    public static void Pulse(float lowFrequency, float highFrequency, float seconds)
    {
        if (!PimlrSettings.Rumble || Gamepad.current == null || !PimlrInput.UsingGamepad) return;
        var r = Instance;
        if (Time.unscaledTime < r.until)
        {
            r.low = Mathf.Max(r.low, lowFrequency);
            r.high = Mathf.Max(r.high, highFrequency);
            r.until = Mathf.Max(r.until, Time.unscaledTime + seconds);
        }
        else { r.low = lowFrequency; r.high = highFrequency; r.until = Time.unscaledTime + seconds; }
    }

    public static void StopAll() { if (instance) instance.until = 0f; }

    void Update()
    {
        var pad = Gamepad.current;
        bool on = pad != null && PimlrSettings.Rumble && Time.unscaledTime < until && Time.timeScale > 0f;
        if (on) { pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high)); motorsOn = true; }
        else if (motorsOn) { if (pad != null) pad.SetMotorSpeeds(0f, 0f); motorsOn = false; }
    }

    void OnDisable() { if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f); }
    void OnApplicationQuit() { if (Gamepad.current != null) Gamepad.current.ResetHaptics(); }
}
