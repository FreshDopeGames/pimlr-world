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
    [SerializeField] private float throwSpeed = 28f;
    [Tooltip("Extra upward angle so the throw arcs.")]
    [SerializeField] private float upwardFactor = 0.5f;
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
                if (heldBalloonVisual != null) heldBalloonVisual.SetActive(true);
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

    // Waits until the Throw clip reaches the release point. Falls back to a timer if the Animator never enters the state.
    private IEnumerator WaitForRelease(Transform player)
    {
        float elapsed = 0f;
        bool sawState = false;

        while (elapsed < 2.5f)
        {
            if (health != null && health.IsDead) yield break;
            if (faceTargetWhileThrowing) FaceTarget(player);

            if (TryGetThrowProgress(out float progress))
            {
                sawState = true;
                if (progress >= releaseNormalizedTime) yield break;
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

    private void ReleaseBalloon(Transform player)
    {
        Vector3 origin = throwPosition != null ? throwPosition.position : transform.position + Vector3.up * 1.5f;
        Vector3 aimPoint = player.position + Vector3.up * aimHeightOffset;

        GameObject balloon = Instantiate(grenadePrefab, origin + transform.forward * 0.25f, transform.rotation);

        if (!balloon.TryGetComponent(out Rigidbody rb))
        {
            Debug.LogWarning("GrenadeThrower: " + grenadePrefab.name + " has no Rigidbody, adding one.", this);
            rb = balloon.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;

        // Never pop on the bomber that threw it.
        foreach (Collider a in balloon.GetComponentsInChildren<Collider>())
            foreach (Collider b in ownColliders)
                if (a != null && b != null) Physics.IgnoreCollision(a, b, true);

        Vector3 dir = (aimPoint - origin).normalized;
        Vector3 launch = new Vector3(dir.x, dir.y + upwardFactor, dir.z).normalized;
        rb.AddForce(launch * throwSpeed, ForceMode.VelocityChange);
    }
}