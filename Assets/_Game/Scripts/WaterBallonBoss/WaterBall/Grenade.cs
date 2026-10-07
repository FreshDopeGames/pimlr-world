using UnityEngine;

public class Grenade : MonoBehaviour
{
    [Header("Impact Effect")]
    [Tooltip("Particle prefab spawned on impact. Drag the water splash here.")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [Tooltip("Offset from the impact point, in the surface's space. Use 0,0,0 for a splash on the ground.")]
    [SerializeField] private Vector3 explosionParticleOffset = new Vector3(0, 1, 0);
    [SerializeField] private float effectLifetime = 4f;
    [SerializeField] private bool alignEffectToSurface = true;
    [SerializeField] private GameObject audioSourcePrefab;

    [Header("Explosion Setting")]
    [Tooltip("Untick so only an impact pops it.")]
    [SerializeField] private bool explodeOnTimer = true;
    [SerializeField] private float explosionDelay = 3f;
    [Tooltip("Impacts earlier than this are ignored.")]
    [SerializeField] private float armDelay = 0.1f;
    [Tooltip("A balloon that never hits anything is removed after this many seconds.")]
    [SerializeField] private float maxLifetime = 10f;
    [SerializeField] private float explosionForce = 700f;
    [SerializeField] private float explosionRadius = 5f;

    [Header("Audio Effects")]
    [SerializeField] private bool playExplosionSound = false;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private AudioClip impactSound;

    private float age;
    private bool hasExploded;

    private void Update()
    {
        if (hasExploded) return;
        age += Time.deltaTime;

        if (explodeOnTimer && age >= explosionDelay)
            Explode(transform.position, Vector3.up);
        else if (age >= maxLifetime)
            Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (age < armDelay) return;
        ContactPoint contact = collision.GetContact(0);
        Explode(contact.point, contact.normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (age < armDelay || other.isTrigger) return;
        Explode(transform.position, Vector3.up);
    }

    private void Explode(Vector3 point, Vector3 normal)
    {
        if (hasExploded) return;
        hasExploded = true;

        bool zone2Done = GameExecutionManager.Instance != null && GameExecutionManager.Instance.zone2Finish;
        if (!zone2Done)
        {
            SpawnEffect(point, normal);
            if (playExplosionSound && explosionSound != null)
                AudioSource.PlayClipAtPoint(explosionSound, point);
            NearbyForceApply(point);
        }

        Destroy(gameObject);
    }

    private void SpawnEffect(Vector3 point, Vector3 normal)
    {
        if (explosionEffectPrefab == null)
        {
            Debug.LogWarning("Grenade: no explosionEffectPrefab assigned on " + name + ".", this);
            return;
        }

        Quaternion rotation = alignEffectToSurface && normal.sqrMagnitude > 0.001f
            ? Quaternion.FromToRotation(Vector3.up, normal)
            : Quaternion.identity;

        GameObject effect = Instantiate(explosionEffectPrefab, point + rotation * explosionParticleOffset, rotation);
        Destroy(effect, effectLifetime);
    }

    private void NearbyForceApply(Vector3 point)
    {
        Collider[] colliders = Physics.OverlapSphere(point, explosionRadius);
        foreach (Collider nearbyObject in colliders)
        {
            if (!nearbyObject.CompareTag("Player")) continue;

            Rigidbody rb = nearbyObject.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddExplosionForce(explosionForce, point, explosionRadius);
        }
    }
}
