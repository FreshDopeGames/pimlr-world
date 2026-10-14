using JUTPS;
using JUTPS.AI;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PIMLR (Infinite Mode): spawned enemies hunt the player instead of wandering. Melee zombies only notice the
/// player inside their field of view (12 m / 40 degrees on the Infinite prefab), and waves arrive 28-55 m away
/// down the street, so this steers them at the player until their own AI takes over (sees the player and attacks).
///  - Wider senses, so they lock on from down the street.
///  - Faster path refresh (JUTPS default is once a second, so chasers followed a path to where you WERE).
///  - While still far away, each hunter aims at a spot on a small ring around the player ON ITS OWN SIDE (the
///    direction it is approaching from), nudged apart from neighbours approaching from a similar direction.
///    The swarm fans out and surrounds you, and nobody's target is ever on the far side of you, so nobody
///    paths straight through you or the pack to reach it. (A fixed random slot did exactly that.)
/// Added by PimlrEnemySpawnDirector to enemies arriving during an Infinite run.
/// </summary>
public class PimlrHuntPlayer : MonoBehaviour
{
    public float repathSeconds = 0.35f;
    public float minViewRadius = 25f;
    public float minViewAngle = 120f;
    [Tooltip("Hunters farther than this aim at their ring slot; closer than this they go straight for the player.")]
    public float spreadUntilDistance = 6f;
    public float ringRadius = 1.8f;
    [Tooltip("Hunters approaching from directions closer than this (degrees) push their ring spots apart.")]
    public float minSpacingDegrees = 30f;
    [Tooltip("How far (degrees) a hunter's ring spot may be pushed from its own approach direction.")]
    public float maxShiftDegrees = 50f;

    static readonly System.Collections.Generic.List<PimlrHuntPlayer> hunters = new System.Collections.Generic.List<PimlrHuntPlayer>();

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

    void OnEnable() { if (!hunters.Contains(this)) hunters.Add(this); }
    void OnDisable() { hunters.Remove(this); }

    void Start()
    {
        // Brain.Awake starts its own once-a-second path refresh; tighten it so the path follows a moving player.
        if (brain) brain.SetPathCalculationRefreshRate(repathSeconds);
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
        Vector3 target = player.position;
        if ((transform.position - target).sqrMagnitude > spreadUntilDistance * spreadUntilDistance)
            target += RingOffset();

        brain.Destination = NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas) ? hit.position
                          : NavMesh.SamplePosition(player.position, out hit, 4f, NavMesh.AllAreas) ? hit.position
                          : player.position;
    }

    /// <summary>Bearing (degrees) of this hunter as seen from the player, on the ground plane.</summary>
    float BearingFrom(Vector3 center)
    {
        Vector3 d = transform.position - center;
        return Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// A spot on the ring on this hunter's own side of the player. Neighbours approaching from a similar
    /// direction push each other's spots apart (symmetrically, so their left-to-right order never flips and
    /// their paths never cross), up to maxShiftDegrees from the hunter's own approach direction.
    /// </summary>
    Vector3 RingOffset()
    {
        float mine = BearingFrom(player.position);
        float shift = 0f;
        for (int i = 0; i < hunters.Count; i++)
        {
            PimlrHuntPlayer other = hunters[i];
            if (other == this || !other || !other.isActiveAndEnabled) continue;
            if ((other.transform.position - player.position).sqrMagnitude <= spreadUntilDistance * spreadUntilDistance) continue;

            float delta = Mathf.DeltaAngle(other.BearingFrom(player.position), mine); // + means I'm counter-clockwise of them
            float abs = Mathf.Abs(delta);
            if (abs >= minSpacingDegrees) continue;

            float push = (minSpacingDegrees - abs) * 0.5f;
            float side = delta > 0f ? 1f : delta < 0f ? -1f : (GetInstanceID() > other.GetInstanceID() ? 1f : -1f);
            shift += push * side;
        }
        shift = Mathf.Clamp(shift, -maxShiftDegrees, maxShiftDegrees);

        float a = (mine + shift) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius;
    }
}
