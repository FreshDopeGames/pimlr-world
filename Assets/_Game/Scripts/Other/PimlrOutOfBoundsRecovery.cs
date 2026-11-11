using JUTPS;
using JUTPS.ActionScripts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PIMLR: safety net for YannicksWorld. If the player (or the vehicle they're driving) ends up below the city
/// floor - squeezed through a wall seam, launched over a wall, fell through a gap - put them back at the last
/// spot where they were safely on the ground instead of letting them die on the bottom plane.
/// Auto-installs in YannicksWorld. To tune, add this component to an object in the scene yourself; the
/// auto-install then skips.
/// </summary>
public class PimlrOutOfBoundsRecovery : MonoBehaviour
{
    [Tooltip("World Y of the city's ground. Walls sit at Y = 0.")]
    public float floorY = 0f;
    [Tooltip("How far below floorY counts as out of the world.")]
    public float fallMargin = 6f;
    [Tooltip("Seconds between 'last safe position' samples.")]
    public float sampleInterval = 0.25f;
    [Tooltip("Lift above the saved position when restoring, so nothing spawns inside the ground.")]
    public float restoreLift = 1.0f;

    const string SceneName = "YannicksWorld";

    JUCharacterController player;
    DriveVehicles driver;
    Vector3 safePos;
    Quaternion safeRot = Quaternion.identity;
    bool hasSafe;
    bool safeWasVehicle;
    float nextSample;

    float KillY => floorY - fallMargin;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName) return;
        if (FindAnyObjectByType<PimlrOutOfBoundsRecovery>() != null) return;
        var go = new GameObject("PIMLR_OutOfBoundsRecovery");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<PimlrOutOfBoundsRecovery>();
    }

    void Update()
    {
        if (!player)
        {
            var p = GameObject.FindWithTag("Player");
            if (!p) return;
            player = p.GetComponent<JUCharacterController>();
            driver = p.GetComponent<DriveVehicles>();
            if (!player) return;
        }
        if (player.IsDead) return;

        bool driving = driver && driver.IsDriving && driver.VehicleToDrive;
        Transform target = driving ? driver.VehicleToDrive.transform : player.transform;
        Rigidbody body = driving ? driver.VehicleToDrive.rb : player.rb;

        // Fast vehicles can tunnel through a 2.8 m wall in one physics step; continuous detection stops that.
        if (driving && body && body.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (target.position.y < KillY)
        {
            Restore(target, body, driving);
            return;
        }

        if (Time.time < nextSample) return;
        nextSample = Time.time + sampleInterval;

        // Only remember spots that are clearly on the city ground and not mid-air.
        bool settled = body ? Mathf.Abs(body.linearVelocity.y) < 1.5f : true;
        bool grounded = driving || player.IsGrounded;
        if (settled && grounded && target.position.y > floorY - 1f)
        {
            safePos = target.position;
            safeRot = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            safeWasVehicle = driving;
            hasSafe = true;
        }
    }

    void Restore(Transform target, Rigidbody body, bool driving)
    {
        if (!hasSafe)
        {
            Debug.LogWarning("PIMLR OutOfBounds: fell out of the world before any safe position was recorded.", this);
            return;
        }

        // A vehicle saved position was usually taken mid-road at speed; nudge back the way it came a little.
        Vector3 pos = safePos + Vector3.up * restoreLift;
        if (driving && safeWasVehicle) pos -= safeRot * Vector3.forward * 3f;

        if (body)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = pos;
            body.rotation = driving ? safeRot : body.rotation;
        }
        target.position = pos;
        if (driving) target.rotation = safeRot;

        Physics.SyncTransforms();
        Debug.Log($"PIMLR OutOfBounds: {(driving ? "vehicle" : "player")} fell below Y {KillY:0.#}; restored to {pos}.", this);
    }
}
