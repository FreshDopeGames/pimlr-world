using JUTPS;
using JUTPS.AI;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PIMLR: movement for gun-carrying PatrolAI enemies (the Big Boss gunner). PatrolAI keeps doing the aiming and
/// shooting; this script runs after it each frame and replaces its movement:
///   - Engaged (player close and in sight): stays about 2.5 m from the player, facing them, and keeps working the
///     range - stepping in and backing off between ~1.8 m and ~3.6 m with a little side-step - instead of running
///     into point-blank. Paths around cars and buildings when the player is farther away.
///   - Not engaged: patrols the nearby area - walks to random points on the NavMesh, pauses, picks another.
///     In Infinite mode the patrol area follows the player's neighbourhood so the boss still finds you.
///   - Stuck detection: if it means to move but hasn't for a second, it side-steps around the obstacle.
/// Attached automatically by PimlrEnemySpawnDirector to arriving enemies that have PatrolAI + a gun.
/// </summary>
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(JUCharacterController))]
public class PimlrGunnerAI : MonoBehaviour
{
    [Header("Engagement")]
    public float engageRadius = 18f;
    [Tooltip("Gives up the chase when the player gets this far away.")]
    public float disengageRadius = 28f;
    [Tooltip("Keeps hunting the last seen spot this long after losing sight.")]
    public float forgetSeconds = 4f;
    public float preferredRange = 2.5f;
    [Tooltip("Range it steps in to / backs off to while working the fight.")]
    public float minRange = 1.8f, maxRange = 3.6f;
    public float rangeChangeMin = 1.2f, rangeChangeMax = 2.8f;
    [Tooltip("Farther than this it paths (around cars/buildings) instead of steering straight.")]
    public float pathBeyond = 7f;

    [Header("Patrol")]
    public float patrolRadius = 14f;
    public float patrolPauseMin = 1f, patrolPauseMax = 3f;
    public float patrolLegTimeout = 12f;

    [Header("Steering")]
    public float steerResponsiveness = 4f;
    public float probeDistance = 1.5f;
    public string playerTag = "Player";

    /// <summary>Centre of the patrol area in Story mode (set by the spawn director; defaults to where it arrived).</summary>
    [HideInInspector] public Vector3 patrolCenter;
    [HideInInspector] public bool followPlayerArea;

    JUCharacterController character;
    JUCharacterArtificialInteligenceBrain brain;
    AIRandomDestination randomDestination;
    Transform player;
    Transform playerChest;

    bool engaged;
    float lastSeenTime = -999f;
    Vector3 lastSeenPos;
    float targetRange, nextRangeChange;
    int sideSign = 1;
    Vector3 steer;

    Vector3 patrolPoint;
    bool hasPatrolPoint;
    float patrolLegEnds, pauseUntil;

    Vector3 stuckCheckPos;
    float stuckCheckTime;
    Vector3 detourDir;
    float detourUntil;

    public static bool Qualifies(GameObject enemy)
    {
        if (!enemy) return false;
        if (!(enemy.GetComponent<JUCharacterArtificialInteligenceBrain>() is PatrolAI)) return false;
        if (enemy.GetComponent<PimlrBomberAI>() || enemy.GetComponent<GrenadeThrower>()) return false;
        return enemy.GetComponentInChildren<JUTPS.WeaponSystem.Weapon>(true) != null;
    }

    public static PimlrGunnerAI Attach(GameObject enemy, Vector3 patrolCenter, bool followPlayerArea)
    {
        if (!Qualifies(enemy)) return null;
        var g = enemy.GetComponent<PimlrGunnerAI>();
        if (!g) g = enemy.AddComponent<PimlrGunnerAI>();
        g.patrolCenter = patrolCenter;
        g.followPlayerArea = followPlayerArea;
        g.hasPatrolPoint = false;
        return g;
    }

    /// <summary>Scene-placed gunners (e.g. the Story Big Boss in YannicksWorld) - called on every scene load.</summary>
    public static void InstallAll()
    {
        bool infinite = RunStats.Active && RunStats.Mode == RunMode.Infinite;
        foreach (var patrol in FindObjectsOfType<PatrolAI>(true))
        {
            GameObject go = patrol.gameObject;
            if (go.CompareTag("Player") || go.GetComponent<PimlrGunnerAI>()) continue;
            if (patrol.WaypointPath != null) continue; // keep hand-authored patrol routes as designed
            Attach(go, go.transform.position, infinite);
        }
    }

    void Awake()
    {
        character = GetComponent<JUCharacterController>();
        brain = GetComponent<JUCharacterArtificialInteligenceBrain>();
        randomDestination = GetComponent<AIRandomDestination>();
        if (patrolCenter == Vector3.zero) patrolCenter = transform.position;
        sideSign = Random.value < 0.5f ? -1 : 1;
        targetRange = preferredRange;
    }

    void Start()
    {
        if (brain) brain.SetPathCalculationRefreshRate(0.4f);
    }

    void Update()
    {
        if (character.IsDead) { enabled = false; return; }
        if (!brain) return;
        if (randomDestination && randomDestination.enabled) randomDestination.enabled = false;

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (!p) return;
            player = p.transform;
            var anim = p.GetComponent<Animator>();
            playerChest = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Chest) : null;
        }

        Vector3 toPlayer = player.position - transform.position; toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        // ---- engaged or not ----
        bool canSee = dist <= disengageRadius && HasLineOfSight();
        if (canSee) { lastSeenTime = Time.time; lastSeenPos = player.position; }

        if (!engaged && dist <= engageRadius && canSee) engaged = true;
        else if (engaged && (dist > disengageRadius || Time.time - lastSeenTime > forgetSeconds)) { engaged = false; hasPatrolPoint = false; }

        if (engaged) Engage(dist, toPlayer, canSee);
        else Patrol();
    }

    // ------------------------------------------------------------ engaged

    void Engage(float dist, Vector3 toPlayer, bool canSee)
    {
        // Face the player while moving: firing-mode locomotion plays the strafe / walk-back clips, and PatrolAI
        // (which runs before us) does the actual shooting.
        Vector3 aimAt = playerChest ? playerChest.position : player.position + Vector3.up * 1.3f;
        character.FiringMode = true;
        character.FiringModeIK = true;
        character.LookAtPosition = aimAt;

        if (!canSee)
        {
            // Lost sight: go to where it last saw you (around obstacles).
            MoveAlongPath(lastSeenPos, 1.5f, dist > 6f);
            return;
        }

        if (Time.time >= nextRangeChange)
        {
            nextRangeChange = Time.time + Random.Range(rangeChangeMin, rangeChangeMax);
            // Mostly around the preferred range, sometimes a step in or a step back.
            targetRange = Random.value < 0.5f ? preferredRange + Random.Range(-0.4f, 0.4f) : Random.Range(minRange, maxRange);
            if (Random.value < 0.35f) sideSign = -sideSign;
        }

        bool directRoute = dist <= pathBeyond && !NavMesh.Raycast(transform.position, player.position, out _, NavMesh.AllAreas);
        if (!directRoute)
        {
            // Far away or something (a car, a wall) between us: path to a spot at range on our side of the player.
            Vector3 dirFromPlayer = dist > 0.01f ? -toPlayer / dist : -transform.forward;
            Vector3 spot = player.position + dirFromPlayer * targetRange;
            MoveAlongPath(OnNavMesh(spot, out var s) ? s : player.position, 1f, dist > pathBeyond);
            return;
        }

        // Close and clear: steer to keep the range, with a gentle side-step so it isn't a static turret.
        Vector3 toward = toPlayer / Mathf.Max(dist, 0.01f);
        Vector3 side = Vector3.Cross(Vector3.up, toward) * sideSign;
        float err = dist - targetRange;                       // + too far, - too close
        float radial = Mathf.Abs(err) < 0.25f ? 0f : Mathf.Clamp(err, -1f, 1f);
        Vector3 desired = toward * radial + side * 0.3f;
        if (desired.sqrMagnitude > 1f) desired.Normalize();

        if (!Clear(desired))
        {
            sideSign = -sideSign;
            desired = toward * radial + Vector3.Cross(Vector3.up, toward) * sideSign * 0.6f;
            if (!Clear(desired)) desired = Vector3.zero;
        }
        Steer(desired, false);
    }

    // ------------------------------------------------------------ patrol

    void Patrol()
    {
        // Patrol walk: face where it's going (no aim pose).
        character.FiringMode = false;
        character.FiringModeIK = false;

        if (Time.time < pauseUntil) { brain.IdleState(); ResetStuck(); return; }

        if (!hasPatrolPoint || Time.time > patrolLegEnds)
        {
            Vector3 center = followPlayerArea && player ? player.position : patrolCenter;
            hasPatrolPoint = PickPatrolPoint(center, out patrolPoint);
            patrolLegEnds = Time.time + patrolLegTimeout;
            if (!hasPatrolPoint) { pauseUntil = Time.time + 1f; return; }
        }

        Vector3 to = patrolPoint - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 1.5f * 1.5f)
        {
            hasPatrolPoint = false;
            pauseUntil = Time.time + Random.Range(patrolPauseMin, patrolPauseMax);
            brain.IdleState();
            return;
        }
        MoveAlongPath(patrolPoint, 1.2f, false);
    }

    bool PickPatrolPoint(Vector3 center, out Vector3 point)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 r = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = center + new Vector3(r.x, 0f, r.y);
            if ((candidate - transform.position).sqrMagnitude < 5f * 5f) continue; // make each leg a real walk
            if (OnNavMesh(candidate, out point)) return true;
        }
        point = transform.position;
        return false;
    }

    // ------------------------------------------------------------ movement helpers

    void MoveAlongPath(Vector3 target, float stop, bool run)
    {
        steer = Vector3.zero;
        if (Detouring(run)) return;
        brain.GoToPosition(target, stop, run);
        CheckStuck(true);
    }

    void Steer(Vector3 desired, bool run)
    {
        if (Detouring(run)) return;
        steer = Vector3.MoveTowards(steer, desired, steerResponsiveness * Time.deltaTime);
        if (steer.sqrMagnitude < 0.0025f) { brain.IdleState(); ResetStuck(); return; }
        character._Move(steer.x, steer.z, run);
        CheckStuck(true);
    }

    /// <summary>If it intends to move but has barely moved for a second (wedged on a car, a pole...), side-step.</summary>
    void CheckStuck(bool intendsToMove)
    {
        if (!intendsToMove) { ResetStuck(); return; }
        if (Time.time - stuckCheckTime < 1f) return;
        float moved = (transform.position - stuckCheckPos).sqrMagnitude;
        stuckCheckPos = transform.position;
        stuckCheckTime = Time.time;
        if (moved > 0.3f * 0.3f) return;

        Vector3 fwd = character.DirectionTransform ? character.DirectionTransform.forward : transform.forward;
        fwd.y = 0f; fwd.Normalize();
        Vector3 left = Vector3.Cross(Vector3.up, fwd);
        Vector3[] options = { left * sideSign, -left * sideSign, (-fwd + left * sideSign).normalized, -fwd };
        foreach (var o in options)
            if (Clear(o)) { detourDir = o; detourUntil = Time.time + 0.9f; return; }
    }

    bool Detouring(bool run)
    {
        if (Time.time >= detourUntil) return false;
        character._Move(detourDir.x, detourDir.z, run);
        return true;
    }

    void ResetStuck() { stuckCheckPos = transform.position; stuckCheckTime = Time.time; }

    bool HasLineOfSight()
    {
        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 target = playerChest ? playerChest.position : player.position + Vector3.up * 1.3f;
        // Default layer only = buildings / walls / props; characters and vehicles don't block "seeing" you.
        return !Physics.Linecast(eye, target, 1 << 0, QueryTriggerInteraction.Ignore);
    }

    bool Clear(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return true;
        Vector3 from = transform.position;
        return !NavMesh.Raycast(from, from + dir.normalized * probeDistance, out _, NavMesh.AllAreas);
    }

    static bool OnNavMesh(Vector3 p, out Vector3 result)
    {
        if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) { result = hit.position; return true; }
        result = p;
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, engageRadius);
        if (hasPatrolPoint && !engaged) { Gizmos.color = Color.cyan; Gizmos.DrawLine(transform.position, patrolPoint); }
    }
#endif
}
