// PIMLR (Nov 16): the bikes get to their top speed twice as fast, forward and in reverse, with the SAME top speed.
//
// The JUTPS bike (MotorcycleController, Torque Force 20) approaches its top speed (~28 m/s on flat ground) on a slow
// exponential curve. Simply doubling the torque would also double the top speed. Instead, each physics step this
// measures how much speed the bike's own physics just gained in the throttle direction and adds the same amount again
// (AccelerationMultiplier - 1 times). The speed curve is therefore the original one played back twice as fast:
// same shape, same top speed, half the time to reach any speed. Braking, coasting, steering, collisions and airtime
// are untouched; only speed gained under throttle, on the ground, in the direction of travel is boosted.
//
// Added to every MotorcycleController on scene load. Tune with PimlrBikeBoost.AccelerationMultiplier (1 = stock).
//
// Unreal note: on a Chaos wheeled vehicle scale the engine torque curve (and drag with it), or simply halve the
// time axis of the throttle response; for a custom bike pawn, multiply the acceleration term in the movement
// component while leaving MaxSpeed alone.

using JUTPS.VehicleSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class PimlrBikeBoost : MonoBehaviour
{
    /// <summary>2 = reaches top speed twice as fast. 1 = stock JUTPS behaviour.</summary>
    public static float AccelerationMultiplier = 2f;

    MotorcycleController bike;
    Rigidbody body;
    float lastSpeed;
    bool tracking;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Sweep();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Sweep();

    static void Sweep()
    {
        foreach (var b in FindObjectsByType<MotorcycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b && !b.GetComponent<PimlrBikeBoost>()) b.gameObject.AddComponent<PimlrBikeBoost>();
    }

    void Awake()
    {
        bike = GetComponent<MotorcycleController>();
        body = GetComponent<Rigidbody>();
    }

    void OnEnable() => tracking = false;

    void FixedUpdate()
    {
        if (!bike || !body || body.isKinematic) { tracking = false; return; }

        Vector3 fwd = transform.forward;
        float speed = Vector3.Dot(body.linearVelocity, fwd);
        float throttle = bike.GetVerticalInput();
        bool driving = bike.IsOn && bike.GroundCheck.IsGrounded && !bike.GetBrakeInput() && Mathf.Abs(throttle) > 0.05f;

        if (driving && tracking && AccelerationMultiplier > 1f)
        {
            float dir = Mathf.Sign(throttle);
            float gained = (speed - lastSpeed) * dir;          // speed the bike's own physics added since the last step
            bool sameWay = speed * dir > -0.5f;                // not while still rolling the other way (that's braking)
            if (gained > 0f && sameWay)
            {
                // Sanity cap: never add more than 1 g worth in a step (a bump or a ramp can spike the measured gain).
                float extra = Mathf.Min(gained * (AccelerationMultiplier - 1f), 9.81f * Time.fixedDeltaTime);
                body.linearVelocity += fwd * (extra * dir);
                speed += extra * dir;
            }
        }

        lastSpeed = speed;
        tracking = driving;
    }
}
