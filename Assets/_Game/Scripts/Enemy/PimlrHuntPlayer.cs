using JUTPS;
using JUTPS.AI;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PIMLR (Infinite Mode): spawned enemies walk toward the player instead of wandering. Melee zombies only notice
/// the player inside their field of view (12 m / 40 degrees on the Infinite prefab), and now that waves arrive
/// 28-55 m away down the street they never got close enough to attack. This steers them at the player until
/// their own AI takes over (sees the player and attacks), and widens their senses a bit.
/// Added by PimlrEnemySpawnDirector to enemies arriving during an Infinite run.
/// </summary>
public class PimlrHuntPlayer : MonoBehaviour
{
    public float repathSeconds = 1f;
    public float minViewRadius = 25f;
    public float minViewAngle = 120f;

    JUCharacterArtificialInteligenceBrain brain;
    AIRandomDestination randomDestination;
    JUHealth health;
    Transform player;
    float nextRepath;

    public static void Attach(GameObject enemy)
    {
        if (!enemy || enemy.GetComponent<JUCharacterArtificialInteligenceBrain>() == null) return;
        if (enemy.GetComponent<PimlrBomberAI>() != null) return; // bombers have their own spacing logic
        if (enemy.GetComponent<PimlrHuntPlayer>() == null) enemy.AddComponent<PimlrHuntPlayer>();
    }

    void Awake()
    {
        brain = GetComponent<JUCharacterArtificialInteligenceBrain>();
        randomDestination = GetComponent<AIRandomDestination>();
        health = GetComponent<JUHealth>();

        // Wider senses so they lock on once they're down the street, not only when already at your elbow.
        if (brain is ZombieAI z && z.FieldOfView != null)
        {
            z.FieldOfView.Radious = Mathf.Max(z.FieldOfView.Radious, minViewRadius);
            z.FieldOfView.Angle = Mathf.Max(z.FieldOfView.Angle, minViewAngle);
        }
        else if (brain is PatrolAI p && p.FieldOfView != null)
        {
            p.FieldOfView.Radious = Mathf.Max(p.FieldOfView.Radious, minViewRadius);
            p.FieldOfView.Angle = Mathf.Max(p.FieldOfView.Angle, minViewAngle);
        }
    }

    void Update()
    {
        if (health && health.IsDead) { enabled = false; return; }
        if (!brain) return;
        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
            if (!player) return;
        }

        if (randomDestination && randomDestination.enabled) randomDestination.enabled = false;
        if (Time.time < nextRepath) return;
        nextRepath = Time.time + repathSeconds;

        // ZombieAI / PatrolAI walk to Destination whenever they don't currently see a target.
        brain.Destination = NavMesh.SamplePosition(player.position, out var hit, 4f, NavMesh.AllAreas) ? hit.position : player.position;
    }
}
