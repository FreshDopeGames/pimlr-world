using System.Collections.Generic;
using UnityEngine;
using JUTPS;

// PIMLR (playtest): one-shot splash damage that only ever hurts the player.
// Put it on the root of the Water_Explosion prefab. It applies damage once, when the effect spawns.
public class WaterSplashDamage : MonoBehaviour
{
    [SerializeField] private float damage = 20f;
    [SerializeField] private float radius = 4f;
    [Tooltip("Damage at the edge of the radius as a fraction of full damage. 1 = no falloff.")]
    [Range(0f, 1f)] [SerializeField] private float edgeDamageFraction = 0.25f;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("World-space offset from the effect's pivot to the centre of the blast.")]
    [SerializeField] private Vector3 centerOffset = Vector3.zero;

    private void Start()
    {
        Vector3 center = transform.position + centerOffset;
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);

        // The player has many colliders, so keep each character's closest distance and hurt them once.
        Dictionary<JUHealth, float> closest = new Dictionary<JUHealth, float>();
        foreach (Collider col in hits)
        {
            JUHealth health = col.GetComponentInParent<JUHealth>();
            if (health == null || health.IsDead) continue;
            if (!health.CompareTag(playerTag) && !col.CompareTag(playerTag)) continue;   // player only

            float distance = Vector3.Distance(center, col.ClosestPoint(center));
            if (!closest.TryGetValue(health, out float best) || distance < best)
                closest[health] = distance;
        }

        foreach (KeyValuePair<JUHealth, float> pair in closest)
        {
            float t = Mathf.Clamp01(pair.Value / radius);
            float amount = damage * Mathf.Lerp(1f, edgeDamageFraction, t);
            pair.Key.DoDamage(amount);   // no hitPosition, so no blood particle on a water hit
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + centerOffset, radius);
    }
}
