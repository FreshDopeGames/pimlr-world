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
    public float repathInterval = 0.8f;
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
    Vector3 steer;            // smoothed world-space move input for back-pedal / circling
    [Header("Speed / facing")]
    [Tooltip("Bomber move speed as a share of the player's normal (non-sprint) on-foot speed, measured live.")]
    [Range(0.3f, 1.2f)] public float speedVsPlayer = 0.9f;
    [Tooltip("How fast it turns to keep facing the player (degrees per second).")]
    public float faceTurnSpeed = 540f;
    [Tooltip("While just circling at throwing range it moves slower than this (share of the speed above).")]
    [Range(0.3f, 1f)] public float circlingPaceShare = 0.65f;
    float baseSpeed = -1f;
    bool facePlayerThisFrame;
    [Header("Steering (back-pedal / circling)")]
    [Tooltip("How quickly the move direction may change (per second). Lower = smoother, less twitchy.")]
    public float steerResponsiveness = 3f;
    [Tooltip("How far ahead (metres) to check the walkable area before stepping that way.")]
    public float probeDistance = 2.5f;
    float stateLockedUntil, strafeFlipAt, blockedFlipUntil;
    [Tooltip("Commit to a state (back off / hold / close in) for at least this long.")]
    public float minStateSeconds = 1.2f;

    void Awake()
    {
        character = GetComponent<JUCharacterController>();
        brain = GetComponent<JUCharacterArtificialInteligenceBrain>();
        patrol = brain as PatrolAI;
        randomDestination = GetComponent<AIRandomDestination>();
        // The bomber prefab ships with LocomotionMode = JuTpsClassic, where JUTPS forces FiringMode = "is holding an
        // item" every physics step. Bombers hold nothing, so facing-the-player mode was switched off constantly:
        // they turned their backs to walk away, and (with root motion walking the body forward) any attempt to
        // face the player made them walk INTO the player. Free mode lets this script own FiringMode.
        if (character.LocomotionMode == JUCharacterController.MovementMode.JuTpsClassic)
            character.LocomotionMode = JUCharacterController.MovementMode.Free;
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
            // Hysteresis + a minimum time per state, so it commits to backing off / closing in instead of
            // flip-flopping at the range boundary (that flip-flop was the twitchy walk).
            if (state != State.Push && Time.time >= stateLockedUntil)
            {
                if (state == State.Retreat) { if (dist > tooClose + 3f) SwitchTo(State.Hold); }
                else if (state == State.Advance) { if (dist < preferredMax - 3f) SwitchTo(State.Hold); }
                else if (dist < tooClose) SwitchTo(State.Retreat);
                else if (dist > preferredMax + 3f) SwitchTo(State.Advance);
                else if (state != State.Hold) SwitchTo(State.Hold);
            }
        }

        // ---- act ----
        // Keep the player in its sights the whole fight: firing-mode locomotion faces LookAtPosition and plays the
        // strafe / back-pedal animations. Only a long run-in from far away faces the way it's running.
        bool faceWhileMoving = !(state == State.Advance && dist > preferredMax + 10f);
        character.FiringMode = faceWhileMoving;
        character.FiringModeIK = false;              // no gun-aim arm pose for an empty-handed thrower
        character.LookAtPosition = player.position + Vector3.up * 1.2f;
        facePlayerThisFrame = faceWhileMoving;
        ApplySpeed();

        // Back-pedalling and circling are steered continuously (no waypoints, no stop-start): the character
        // faces the player while walking, so the Legs layer plays the walk-backwards / strafe clips smoothly.
        if (state == State.Retreat || state == State.Hold)
        {
            hasMoveTarget = false;
            Vector3 desired = SteerDirection(dist, toPlayer);
            steer = Vector3.MoveTowards(steer, desired, steerResponsiveness * Time.deltaTime);
            if (steer.sqrMagnitude < 0.0025f) brain.IdleState();
            else character._Move(steer.x, steer.z, character.IsRunning);
            return;
        }
        steer = Vector3.zero;

        // Closing in (advance / push) uses NavMesh paths, since it may need to go around buildings.
        if (Time.time >= nextRepath || !hasMoveTarget)
        {
            nextRepath = Time.time + repathInterval;
            hasMoveTarget = PickMoveTarget(dist, toPlayer, out moveTarget);
        }

        if (!hasMoveTarget) { brain.IdleState(); return; }

        bool run = (state == State.Advance && dist > preferredMax + 10f) || character.IsRunning;
        const float stop = 1.5f;
        Vector3 to = moveTarget - transform.position; to.y = 0f;
        if (to.sqrMagnitude < stop * stop) { hasMoveTarget = false; return; } // pick the next point right away, no pause

        if (to.magnitude < 12f && !NavMesh.Raycast(transform.position, moveTarget, out _, NavMesh.AllAreas))
        {
            Vector3 d = to.normalized;
            character._Move(d.x, d.z, run);
        }
        else
        {
            brain.GoToPosition(moveTarget, stop, run);
        }
    }

    /// <summary>
    /// World-space walking direction while backing off (Retreat) or circling (Hold), with a short look-ahead on
    /// the NavMesh so it slides along walls and kerbs instead of walking into them and jittering.
    /// </summary>
    Vector3 SteerDirection(float dist, Vector3 toPlayer)
    {
        Vector3 away = dist > 0.01f ? -toPlayer / dist : -transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, -away) * strafeSign;

        Vector3 desired;
        if (state == State.Retreat)
        {
            desired = (away + side * 0.35f).normalized;          // mostly straight back, slightly angled
        }
        else
        {
            if (Time.time >= strafeFlipAt) { strafeSign = -strafeSign; strafeFlipAt = Time.time + Random.Range(2.5f, 4.5f); side = -side; }
            float mid = (tooClose + preferredMax) * 0.5f;
            float radial = Mathf.Clamp((mid - dist) / (preferredMax - tooClose), -0.6f, 0.6f); // drift toward the middle of the band
            desired = (side + away * radial).normalized;
        }

        if (Clear(desired)) return desired;

        // Blocked: try sliding along the obstacle, then the other way, then plain sideways.
        Vector3 slideA = (side + away * 0.3f).normalized;
        if (Clear(slideA)) return slideA;
        Vector3 slideB = (-side + away * 0.3f).normalized;
        if (Clear(slideB))
        {
            // Commit to the new side for a moment so it doesn't flip back and forth every frame along a wall.
            if (Time.time >= blockedFlipUntil) { strafeSign = -strafeSign; blockedFlipUntil = Time.time + 1.5f; }
            return slideB;
        }
        if (Clear(-side)) return -side;
        return Vector3.zero;                                     // cornered: stand and throw
    }

    bool Clear(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return false;
        Vector3 from = transform.position;
        return !NavMesh.Raycast(from, from + dir * probeDistance, out _, NavMesh.AllAreas);
    }

    /// <summary>
    /// Keep facing the player while engaged. Runs after animation and every other script, so nothing that
    /// briefly drops firing mode (a hit reaction, the throw clip, a stumble) can swing its back to you.
    /// </summary>
    void LateUpdate()
    {
        if (!facePlayerThisFrame || !player || character.IsDead || character.IsRagdolled) return;
        character.FiringMode = true;
        Vector3 flat = player.position - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) return;
        Quaternion want = Quaternion.LookRotation(flat.normalized, Vector3.up);
        // The movement-direction helper is a child of the character: keep its world rotation, so turning the body
        // to face the player never bends the direction it's walking.
        Transform dir = character.DirectionTransform;
        Quaternion keep = dir ? dir.rotation : Quaternion.identity;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, faceTurnSpeed * Time.deltaTime);
        if (dir) dir.rotation = keep;
    }

    /// <summary>
    /// Move at speedVsPlayer x the player's measured on-foot speed. In firing-mode locomotion JUTPS moves at
    /// Speed x 0.5 (walk) or Speed x 1.3 (run), so pick the gait whose animation suits the pace and scale Speed.
    /// </summary>
    void ApplySpeed()
    {
        if (baseSpeed < 0f) baseSpeed = character.Speed;
        PimlrPlayerPace.Sample();
        float target = PimlrPlayerPace.OnFootSpeed * speedVsPlayer * (state == State.Hold ? circlingPaceShare : 1f);
        bool jog = target > 2.2f;                       // above a brisk walk, use the running back-pedal clips
        character.IsRunning = jog;
        character.Speed = target / (jog ? 1.3f : 0.5f);
    }

    void SwitchTo(State next)
    {
        if (state == next) return;
        state = next;
        stateLockedUntil = Time.time + minStateSeconds;
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
            facePlayerThisFrame = false;
            if (baseSpeed > 0f) character.Speed = baseSpeed;
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
                // Drift to one side for a few seconds, then the other - circling, not jittering.
                if (Time.time >= strafeFlipAt) { strafeSign = -strafeSign; strafeFlipAt = Time.time + Random.Range(2.5f, 4.5f); }
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
