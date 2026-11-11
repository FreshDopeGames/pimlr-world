using JUTPS.CameraSystems;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PIMLR (post-playtest): random starting spot for each Infinite Mode run.
///
/// SETUP (YannicksWorld):
///   1. Create an empty GameObject named exactly "PIMLR_InfiniteStartPoints" (position doesn't matter).
///   2. Add empty child GameObjects where a run may start - open street spots on the NavMesh, with room for
///      enemies to arrive from several directions. Rotate each one so its blue Z arrow points the way the
///      player should face. 8-12 spread around the city is plenty.
///   3. Done. Each run picks one at random (never the same as last run). With no points it starts where it
///      always did. Tip: give each child an icon (Inspector, top-left cube) so they show in the Scene view.
///
/// The Infinite Mode spawner moves with the player, so waves form around the new start (enemies already spawn
/// on a ring around the player via PimlrEnemySpawnDirector).
/// </summary>
public static class PimlrInfiniteStart
{
    public const string RootName = "PIMLR_InfiniteStartPoints";
    const string LastKey = "PIMLR_InfiniteLastStart";

    /// <summary>Called by InfiniteMode.Start before the first wave.</summary>
    public static void Apply(Component infiniteSpawner)
    {
        var root = GameObject.Find(RootName);
        if (root == null || root.transform.childCount == 0) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // Random point, avoiding last run's (if there's more than one).
        int count = root.transform.childCount;
        int last = PlayerPrefs.GetInt(LastKey, -1);
        int pick = Random.Range(0, count);
        if (count > 1 && pick == last) pick = (pick + 1 + Random.Range(0, count - 1)) % count;
        PlayerPrefs.SetInt(LastKey, pick);

        Transform point = root.transform.GetChild(pick);
        Vector3 target = point.position;
        if (NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas)) target = hit.position;
        Quaternion facing = Quaternion.Euler(0f, point.eulerAngles.y, 0f);

        Vector3 delta = target - player.transform.position;

        // Teleport the player (Rigidbody-safe) and stop any leftover motion.
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = target;
            rb.rotation = facing;
        }
        player.transform.SetPositionAndRotation(target, facing);

        // Bring the camera rig along so it doesn't swoop across the city, and face it the player's way.
        foreach (var cam in Object.FindObjectsOfType<JUCameraController>())
        {
            cam.transform.position += delta;
            cam.rotytarget = facing.eulerAngles.y;
        }

        // The wave spawner's area is centred on its own transform: move it with the player.
        if (infiniteSpawner != null) infiniteSpawner.transform.position += delta;

        Debug.Log($"PIMLR Infinite: run starts at '{point.name}' ({pick + 1}/{count}).");
    }
}
