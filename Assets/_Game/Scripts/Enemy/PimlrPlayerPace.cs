using JUTPS;
using UnityEngine;

/// <summary>
/// PIMLR: measures how fast the player actually moves on foot (their normal pace, not sprinting, not driving),
/// so enemies can be tuned relative to it ("bombers move at 90% of the player's walk"). The player's walk/jog
/// speed comes from root-motion animation, so it's measured from real movement rather than read from settings.
/// Sampled lazily (once per frame) by whoever asks.
/// </summary>
public static class PimlrPlayerPace
{
    /// <summary>Smoothed on-foot speed in m/s. Starts at a typical JUTPS jog until the player has moved.</summary>
    public static float OnFootSpeed { get; private set; } = 3.2f;

    static JUCharacterController player;
    static Vector3 lastPos;
    static int lastFrame = -1;
    static bool hasLast;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { player = null; hasLast = false; lastFrame = -1; OnFootSpeed = 3.2f; }

    public static void Sample()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            player = go ? go.GetComponent<JUCharacterController>() : null;
            hasLast = false;
            if (player == null) return;
        }

        Vector3 pos = player.transform.position;
        if (hasLast && player.IsMoving && player.IsGrounded && !player.IsSprinting && !player.IsDriving
            && !player.IsRolling && !player.IsJumping && !player.IsDead)
        {
            Vector3 d = pos - lastPos; d.y = 0f;
            float v = d.magnitude / dt;
            if (v > 0.6f && v < 9f)                       // ignore stops, teleports and recovery snaps
                OnFootSpeed = Mathf.Lerp(OnFootSpeed, v, 1.5f * dt);
        }
        lastPos = pos;
        hasLast = true;
    }
}
