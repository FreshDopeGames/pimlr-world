using JUTPS;
using JUTPS.AI;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PIMLR: an enemy that arrived away from the main (Sirihanna) intersection and hasn't found the player within
/// 10 s walks to the intersection instead of wandering the outskirts. Stops seeking as soon as it engages the
/// player (gets close or takes a hit) or arrives. Added by PimlrEnemySpawnDirector to relocated spawns.
/// </summary>
public class PimlrSeekIntersection : MonoBehaviour
{
    public Vector3 intersection;
    public float graceSeconds = 10f;
    [Tooltip("Player this close = engaged (no need to seek).")]
    public float engageDistance = 22f;
    public float arriveRadius = 8f;

    JUCharacterArtificialInteligenceBrain brain;
    AIRandomDestination randomDestination;
    JUHealth health;
    Transform player;
    float spawnTime, lastHealth, nextRepath;
    bool engaged, seeking, done;
    Vector3 target;

    public static void Attach(GameObject enemy, Vector3 intersection)
    {
        if (!enemy || enemy.GetComponent<JUCharacterArtificialInteligenceBrain>() == null) return;
        var s = enemy.GetComponent<PimlrSeekIntersection>();
        if (s == null) s = enemy.AddComponent<PimlrSeekIntersection>();
        s.intersection = intersection;
        s.ResetTimer();
    }

    void Awake()
    {
        brain = GetComponent<JUCharacterArtificialInteligenceBrain>();
        randomDestination = GetComponent<AIRandomDestination>();
        health = GetComponent<JUHealth>();
    }

    void OnEnable() => ResetTimer();

    void ResetTimer()
    {
        spawnTime = Time.time;
        engaged = seeking = done = false;
        lastHealth = health ? health.Health : 0f;
        target = NavMesh.SamplePosition(intersection, out var hit, 10f, NavMesh.AllAreas) ? hit.position : intersection;
    }

    void Update()
    {
        if (done || brain == null) return;
        if (health && health.IsDead) { Finish(); return; }

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // Engaged = the player is near, or we've been shot.
        if (player && (player.position - transform.position).sqrMagnitude < engageDistance * engageDistance) engaged = true;
        if (health && health.Health < lastHealth - 0.01f) engaged = true;
        if (health) lastHealth = health.Health;
        if (engaged) { Finish(); return; }

        if (!seeking)
        {
            if (Time.time - spawnTime < graceSeconds) return;
            seeking = true;
        }

        if ((target - transform.position).sqrMagnitude < arriveRadius * arriveRadius) { Finish(); return; }

        if (randomDestination && randomDestination.enabled) randomDestination.enabled = false;
        if (Time.time >= nextRepath)
        {
            nextRepath = Time.time + 1f;
            brain.Destination = target;   // PatrolAI / ZombieAI follow Destination when they have no target
        }
    }

    void Finish()
    {
        done = true;
        var bomber = GetComponent<PimlrBomberAI>();
        if (seeking && randomDestination && (bomber == null || !bomber.enabled))
            randomDestination.enabled = true;
        seeking = false;
    }
}
