using JUTPS;
using JUTPS.AI;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PIMLR: grenade-thrower ("bomber") behaviour, layered on top of JUTPS PatrolAI.
///   - Too close  -> keeps facing the player and backs away (strafing sideways if a wall is behind it).
///   - In range   -> holds its ground facing the player, drifting sideways a little; GrenadeThrower does the throwing.
///   - Too far    -> runs in to throwing range, so you can't snipe it from across the map.
///   - Every so often it also makes a random "push" closer than it likes, to pressure the player.
/// While engaged it switches PatrolAI / AIRandomDestination off and drives the character itself; when the player is
/// out of the engage radius it hands control back to them.
/// </summary>
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(JUCharacterController))]
public class PimlrBomberAI : MonoBehaviour
{
    [Header("Ranges (metres)")]
    [Tooltip("Closer than this: back away.")]
    public float tooClose = 11f;
    [Tooltip("Preferred throwing distance - it settles between tooClose and this.")]
    public float preferredMax = 26f;
    [Tooltip("Engage the player anywhere inside this radius (otherwise normal patrol).")]
    public float engageRadius = 90f;

    [Header("Random pushes")]
    public float pushIntervalMin = 7f;
    public float pushIntervalMax = 15f;
    [Range(0f, 1f)] public float pushChance = 0.6f;
    [Tooltip("How close a push gets before it falls back again.")]
    public float pushDistance = 9f;
    public float pushMaxDuration = 6f;

    [Header("Movement")]
    public float retreatStep = 7f;
    public float strafeStep = 5f;
    public float repathInterval = 0.4f;
    public string playerTag = "Player";

    enum State { Patrol, Advance, Hold, Retreat, Push }

    JUCharacterController character;
    JUCharacterArtificialInteligenceBrain brain;
    PatrolAI patrol;
    AIRandomDestination randomDestination;
    Transform player;
    State state = State.Patrol;
    Vector3 moveTarget;
    bool hasMoveTarget;
    float nextRepath, nextPushRoll, pushEndTime;
    int strafeSign = 1;

    void Awake()
    {
        character = GetComponent<JUCharacterController>();
        brain = GetComponent<JUCharacterArtificialInteligenceBrain>();
        patrol = brain as PatrolAI;
        randomDestination = GetComponent<AIRandomDestination>();
        strafeSign = Random.value < 0.5f ? -1 : 1;
        nextPushRoll = Time.time + Random.Range(pushIntervalMin, pushIntervalMax);
    }

    void OnDisable() => SetEngaged(false);

    void Update()
    {
        if (character.IsDead) { SetEngaged(false); enabled = false; return; }
        if (brain == null) return;

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p) player = p.transform;
            if (!player) return;
        }

        Vector3 toPlayer = player.position - transform.position; toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        if (dist > engageRadius) { SwitchTo(State.Patrol); return; }

        // ---- pick a state ----
        if (state == State.Push && (Time.time > pushEndTime || dist <= pushDistance))
            state = State.Hold; // push finished, settle back

        if (state != State.Push)
        {
            if (Time.time >= nextPushRoll)
            {
                nextPushRoll = Time.time + Random.Range(pushIntervalMin, pushIntervalMax);
                if (Random.value < pushChance && dist > pushDistance + 2f)
                {
                    SwitchTo(State.Push);
                    pushEndTime = Time.time + pushMaxDuration;
                }
            }
            if (state != State.Push)
            {
                if (dist < tooClose) SwitchTo(State.Retreat);
                else if (dist > preferredMax) SwitchTo(State.Advance);
                else if (state != State.Hold) SwitchTo(State.Hold);
            }
        }

        // ---- act ----
        bool faceWhileMoving = state == State.Retreat || state == State.Hold;
        character.FiringMode = faceWhileMoving;      // firing-mode rotation = keep looking at LookAtPosition
        character.FiringModeIK = false;              // no gun-aim arm pose for an empty-handed thrower
        character.LookAtPosition = player.position + Vector3.up * 1.2f;

        if (Time.time >= nextRepath || !hasMoveTarget)
        {
            nextRepath = Time.time + repathInterval;
            hasMoveTarget = PickMoveTarget(dist, toPlayer, out moveTarget);
        }

        if (!hasMoveTarget) { brain.IdleState(); return; }

        bool run = state == State.Advance || state == State.Push;
        float stop = state == State.Hold ? 1.2f : 1.5f;
        if ((moveTarget - transform.position).sqrMagnitude < stop * stop) { brain.IdleState(); return; }
        brain.GoToPosition(moveTarget, stop, run);
    }

    void SwitchTo(State next)
    {
        if (state == next) return;
        state = next;
        hasMoveTarget = false;
        if (next == State.Hold) strafeSign = Random.value < 0.5f ? -1 : 1;
        SetEngaged(next != State.Patrol);
    }

    void SetEngaged(bool engaged)
    {
        if (patrol) patrol.enabled = !engaged && !(character && character.IsDead);
        if (randomDestination) randomDestination.enabled = !engaged;
        if (!engaged && character && !character.IsDead)
        {
            character.FiringMode = false;
            character.FiringModeIK = false;
        }
    }

    bool PickMoveTarget(float dist, Vector3 toPlayer, out Vector3 target)
    {
        Vector3 dirToPlayer = dist > 0.01f ? toPlayer / dist : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, dirToPlayer) * strafeSign;

        switch (state)
        {
            case State.Advance:
                // Close to the middle of the preferred band, coming in at a slight angle.
                return OnNavMesh(player.position - dirToPlayer * (tooClose + preferredMax) * 0.5f + side * 2f, out target);

            case State.Push:
                return OnNavMesh(player.position - dirToPlayer * pushDistance * 0.8f + side * 3f, out target);

            case State.Retreat:
                if (OnNavMesh(transform.position - dirToPlayer * retreatStep + side * 1.5f, out target)
                    && Vector3.Distance(target, player.position) > dist + 1f)
                    return true;
                // Wall behind us: slide sideways instead.
                strafeSign = -strafeSign;
                return OnNavMesh(transform.position + Vector3.Cross(Vector3.up, dirToPlayer) * strafeSign * strafeStep, out target);

            case State.Hold:
                if (Random.value < 0.15f) strafeSign = -strafeSign;
                return OnNavMesh(transform.position + side * strafeStep * 0.6f, out target);
        }
        target = transform.position;
        return false;
    }

    static bool OnNavMesh(Vector3 p, out Vector3 result)
    {
        if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) { result = hit.position; return true; }
        result = p;
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, tooClose);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, preferredMax);
        if (hasMoveTarget) { Gizmos.color = Color.yellow; Gizmos.DrawLine(transform.position, moveTarget); }
    }
#endif
}
