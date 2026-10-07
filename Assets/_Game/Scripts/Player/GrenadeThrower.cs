using System.Collections;
using UnityEngine;

// PIMLR (playtest): water balloon bomber. Throws the assigned prefab at the player on a timer.
public class GrenadeThrower : MonoBehaviour
{
    [Header("Thrown Object")]
    [Tooltip("The object to throw. Drag the new balloon prefab here.")]
    [SerializeField] private GameObject grenadePrefab;
    [Tooltip("Where the balloon spawns (the hand). Falls back to a point above the bomber.")]
    [SerializeField] private Transform throwPosition;

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
    private Collider[] ownColliders;
    private Coroutine loop;

    private void Awake()
    {
        health = GetComponent<JUTPS.JUHealth>();
        ownColliders = GetComponentsInChildren<Collider>();
    }

    private void OnEnable()
    {
        loop = StartCoroutine(ThrowLoop());
    }

    private void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
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
            bool thrown = TryThrow();
            // retry in a second when nothing was thrown (no player yet, out of range)
            yield return new WaitForSeconds(thrown ? Random.Range(minInterval, maxInterval) : 1f);
        }
    }

    // Kept for any UnityEvent that still calls it.
    public void OnStartThrowing()
    {
        TryThrow();
    }

    private Transform ResolveTarget()
    {
        if (target != null) return target;
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null) target = player.transform;
        return target;
    }

    private bool TryThrow()
    {
        if (grenadePrefab == null) return false;

        Transform player = ResolveTarget();
        if (player == null) return false;

        Vector3 origin = throwPosition != null ? throwPosition.position : transform.position + Vector3.up * 1.5f;
        Vector3 aimPoint = player.position + Vector3.up * aimHeightOffset;

        if (maxThrowDistance > 0f && Vector3.Distance(transform.position, player.position) > maxThrowDistance)
            return false;

        GameObject balloon = Instantiate(grenadePrefab, origin + transform.forward, transform.rotation);

        if (!balloon.TryGetComponent(out Rigidbody rb))
        {
            Debug.LogWarning("GrenadeThrower: " + grenadePrefab.name + " has no Rigidbody, adding one.", this);
            rb = balloon.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;

        // Never pop on the bomber that threw it.
        Collider[] balloonColliders = balloon.GetComponentsInChildren<Collider>();
        foreach (Collider a in balloonColliders)
            foreach (Collider b in ownColliders)
                if (a != null && b != null) Physics.IgnoreCollision(a, b, true);

        Vector3 dir = (aimPoint - origin).normalized;
        Vector3 launch = new Vector3(dir.x, dir.y + upwardFactor, dir.z).normalized;
        rb.AddForce(launch * throwSpeed, ForceMode.VelocityChange);
        return true;
    }
}
