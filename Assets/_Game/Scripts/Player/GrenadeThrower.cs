using System.Collections;
using UnityEngine;

// PIMLR (playtest): water balloon bomber. Plays the Throw animation, then releases the balloon at the player.
public class GrenadeThrower : MonoBehaviour
{
    [Header("Thrown Object")]
    [Tooltip("The object to throw. Drag the new balloon prefab here.")]
    [SerializeField] private GameObject grenadePrefab;
    [Tooltip("Where the balloon spawns (the hand). Falls back to a point above the bomber.")]
    [SerializeField] private Transform throwPosition;

    [Header("Throw Animation")]
    [SerializeField] private bool useThrowAnimation = true;
    [SerializeField] private string throwTriggerName = "Throw";
    [Tooltip("Name of the Animator state that plays Throw.anim (Base Layer).")]
    [SerializeField] private string throwStateName = "Throw";
    [Range(0f, 1f)]
    [Tooltip("How far through the clip the balloon leaves the hand. The stock throw event fires at 0.48.")]
    [SerializeField] private float releaseNormalizedTime = 0.48f;
    [Tooltip("If the Animator never enters the Throw state, release after this many seconds anyway.")]
    [SerializeField] private float fallbackReleaseDelay = 0.8f;
    [SerializeField] private bool faceTargetWhileThrowing = true;
    [SerializeField] private float turnSpeed = 8f;
    [Tooltip("Optional. A mesh-only balloon (no Rigidbody, Collider or Grenade) shown in the hand during the wind-up.")]
    [SerializeField] private GameObject heldBalloonVisual;

    [Header("Aim and Power")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("(Legacy, unused since the arc is now solved to land on the player.)")]
    [SerializeField] private float throwSpeed = 28f;
    [Tooltip("(Legacy, unused since the arc is now solved to land on the player.)")]
    [SerializeField] private float upwardFactor = 0.5f;
    [Tooltip("PIMLR: horizontal speed of the throw (m/s). Flight time = distance / this, clamped below.")]
    [SerializeField] private float arcHorizontalSpeed = 15f;
    [SerializeField] private float minFlightTime = 0.45f;
    [SerializeField] private float maxFlightTime = 1.6f;
    [Tooltip("PIMLR: 0 = aim where the player is, 1 = full lead on where they're running to.")]
    [Range(0f, 1f)] [SerializeField] private float leadFactor = 0.5f;
    [Tooltip("PIMLR: release when the hand's forward swing peaks (synced to the animation). Off = fixed releaseNormalizedTime.")]
    [SerializeField] private bool releaseAtSwingPeak = true;
    [Tooltip("Aims this many metres above the player's pivot.")]
    [SerializeField] private float aimHeightOffset = 1f;
    [Tooltip("Only throws when the player is this close (metres). 0 = no limit.")]
    [SerializeField] private float maxThrowDistance = 40f;

    [Header("Timing")]
    [SerializeField] private float firstThrowDelay = 3f;
    [SerializeField] private float minInterval = 3f;
    [SerializeField] private float maxInterval = 15f;

    private Transform target;
    private JUTPS.JUHealth health;
    private Animator animator;
    private Collider[] ownColliders;
    private Coroutine loop;
    private int throwTriggerHash;
    private bool checkedTrigger;
    private bool hasThrowTrigger;
    private bool isThrowing;
    private bool throwSucceeded;

    private void Awake()
    {
        health = GetComponent<JUTPS.JUHealth>();
        animator = GetComponent<Animator>();
        ownColliders = GetComponentsInChildren<Collider>();
        throwTriggerHash = Animator.StringToHash(throwTriggerName);

        // PIMLR: bombers keep their distance (face + back away, random pushes in). Added here so every bomber
        // prefab/spawn gets it without editing prefabs; add it in the Inspector yourself to tune per prefab.
        if (GetComponent<PimlrBomberAI>() == null && GetComponent<JUTPS.JUCharacterController>() != null)
            gameObject.AddComponent<PimlrBomberAI>();
    }

    private void OnEnable()
    {
        loop = StartCoroutine(ThrowLoop());
    }

    private void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
        isThrowing = false;
        if (heldBalloonVisual != null) heldBalloonVisual.SetActive(false);
        if (held != null) { Destroy(held); held = null; } // PIMLR: don't leave a frozen balloon in the hand
    }

    private IEnumerator ThrowLoop()
    {
        if (grenadePrefab == null)
        {
            Debug.LogError("GrenadeThrower: grenadePrefab is not assigned on " + name + ".", this);
            yield break;
        }

        yield return new WaitForSeconds(firstThrowDelay);

        while (true)
        {
            if (health != null && health.IsDead) yield break;
            yield return ThrowRoutine();
            // retry in a second when nothing was thrown (no player yet, out of range)
            yield return new WaitForSeconds(throwSucceeded ? Random.Range(minInterval, maxInterval) : 1f);
        }
    }

    // Kept for any UnityEvent that still calls it.
    public void OnStartThrowing()
    {
        if (!isThrowing) StartCoroutine(ThrowRoutine());
    }

    private Transform ResolveTarget()
    {
        if (target != null) return target;
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null) target = player.transform;
        return target;
    }

    private IEnumerator ThrowRoutine()
    {
        throwSucceeded = false;
        if (isThrowing || grenadePrefab == null) yield break;

        Transform player = ResolveTarget();
        if (player == null) yield break;
        if (maxThrowDistance > 0f && Vector3.Distance(transform.position, player.position) > maxThrowDistance)
            yield break;

        isThrowing = true;
        try
        {
            if (CanAnimate())
            {
                // PIMLR: the real balloon sits in the hand for the wind-up (physics/fuse off), so it visibly
                // leaves the hand at release instead of popping into existence somewhere near the bomber.
                if (heldBalloonVisual != null) heldBalloonVisual.SetActive(true);
                else held = SpawnHeld();
                animator.ResetTrigger(throwTriggerHash);
                animator.SetTrigger(throwTriggerHash);
                yield return WaitForRelease(player);
                animator.ResetTrigger(throwTriggerHash);   // never leave a stale trigger for later
                if (heldBalloonVisual != null) heldBalloonVisual.SetActive(false);
            }

            if (health != null && health.IsDead) yield break;   // died during the wind-up
            ReleaseBalloon(player);
            throwSucceeded = true;
        }
        finally
        {
            isThrowing = false;
            if (heldBalloonVisual != null) heldBalloonVisual.SetActive(false);
            if (held != null) { Destroy(held); held = null; }   // interrupted before release (died / disabled)
        }
    }

    private bool CanAnimate()
    {
        if (!useThrowAnimation || animator == null || !animator.isActiveAndEnabled) return false;

        if (!checkedTrigger)
        {
            checkedTrigger = true;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == throwTriggerHash && p.type == AnimatorControllerParameterType.Trigger)
                    hasThrowTrigger = true;
            if (!hasThrowTrigger)
                Debug.LogWarning("GrenadeThrower: Animator has no trigger '" + throwTriggerName + "', throwing without animation.", this);
        }
        return hasThrowTrigger;
    }

    // Waits until the balloon should leave the hand. PIMLR: by default that's the moment the throwing hand's
    // forward swing peaks (measured on the actual rig, so it stays in sync whatever the clip or avatar);
    // releaseNormalizedTime is the fallback if no clear peak is seen. Falls back to a timer if the Animator
    // never enters the Throw state.
    private IEnumerator WaitForRelease(Transform player)
    {
        float elapsed = 0f;
        bool sawState = false;
        Transform hand = throwPosition;
        Vector3 lastLocal = hand ? transform.InverseTransformPoint(hand.position) : Vector3.zero;
        float lastSpeed = 0f;

        while (elapsed < 3f)
        {
            if (health != null && health.IsDead) yield break;
            if (faceTargetWhileThrowing) FaceTarget(player);

            if (TryGetThrowProgress(out float progress))
            {
                sawState = true;
                if (releaseAtSwingPeak && hand != null && Time.deltaTime > 0f)
                {
                    // Forward speed of the hand in the bomber's own space (body turning doesn't count).
                    Vector3 local = transform.InverseTransformPoint(hand.position);
                    float speed = (local.z - lastLocal.z) / Time.deltaTime;
                    lastLocal = local;
                    if (progress > 0.2f && lastSpeed > 1.5f && speed < lastSpeed) yield break; // swing just peaked
                    lastSpeed = speed;
                    if (progress >= 0.85f) yield break;                                      // no clear peak: let go
                }
                else if (progress >= releaseNormalizedTime) yield break;
            }
            else if (sawState) yield break;                       // left the Throw state early: release now
            else if (elapsed >= fallbackReleaseDelay) yield break; // never entered it: release anyway

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool TryGetThrowProgress(out float progress)
    {
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName(throwStateName))
        {
            progress = current.normalizedTime;
            return true;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsName(throwStateName))
            {
                progress = next.normalizedTime;
                return true;
            }
        }

        progress = 0f;
        return false;
    }

    private void FaceTarget(Transform player)
    {
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
    }

    private GameObject held;

    /// <summary>The real balloon, parented to the hand with physics, colliders and fuse switched off.</summary>
    private GameObject SpawnHeld()
    {
        if (throwPosition == null) return null;
        GameObject b = Instantiate(grenadePrefab, throwPosition.position, throwPosition.rotation, throwPosition);
        if (b.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        foreach (Collider c in b.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (Grenade g in b.GetComponentsInChildren<Grenade>()) g.enabled = false;   // fuse doesn't tick in the hand
        return b;
    }

    private void ReleaseBalloon(Transform player)
    {
        Vector3 origin = throwPosition != null ? throwPosition.position : transform.position + Vector3.up * 1.5f;

        GameObject balloon = held;
        held = null;
        if (balloon != null)
        {
            balloon.transform.SetParent(null, true);
            foreach (Collider c in balloon.GetComponentsInChildren<Collider>()) c.enabled = true;
            foreach (Grenade gravity in balloon.GetComponentsInChildren<Grenade>()) gravity.enabled = true;
        }
        else
        {
            balloon = Instantiate(grenadePrefab, origin + transform.forward * 0.15f, transform.rotation);
        }
        origin = balloon.transform.position;

        if (!balloon.TryGetComponent(out Rigidbody rb))
        {
            Debug.LogWarning("GrenadeThrower: " + grenadePrefab.name + " has no Rigidbody, adding one.", this);
            rb = balloon.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Never pop on the bomber that threw it.
        foreach (Collider a in balloon.GetComponentsInChildren<Collider>())
            foreach (Collider b in ownColliders)
                if (a != null && b != null) Physics.IgnoreCollision(a, b, true);

        // PIMLR: solve the arc so it actually lands on the player: pick a flight time from the distance, then the
        // launch velocity that reaches the aim point in exactly that time under gravity (drag-free).
        Vector3 aimPoint = player.position + Vector3.up * aimHeightOffset;
        Vector3 flatToAim = aimPoint - origin; flatToAim.y = 0f;
        float t = Mathf.Clamp(flatToAim.magnitude / Mathf.Max(1f, arcHorizontalSpeed), minFlightTime, maxFlightTime);
        if (leadFactor > 0f && player.TryGetComponent(out Rigidbody prb))
        {
            Vector3 pv = prb.linearVelocity; pv.y = 0f;
            aimPoint += pv * t * leadFactor;
        }
        Vector3 g = rb.useGravity ? Physics.gravity : Vector3.zero;
        rb.linearDamping = 0f;
        rb.linearVelocity = (aimPoint - origin) / t - 0.5f * g * t;
        rb.angularVelocity = Random.insideUnitSphere * 4f; // a little tumble
    }
}
