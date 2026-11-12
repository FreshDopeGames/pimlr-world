// PIMLR #STF (Save The Fans) — a body-sized collider on each fan so water bullets hit the fan instead of passing
// through it. FanHeat builds one automatically (child "FanHitbox").
//
//   - Lives on the FanHitbox layer, which only collides with the water bullet layer (Project Settings > Physics), so
//     it never blocks the player, enemies or vehicles, and isn't baked into the NavMesh (Default layer only).
//   - A hit from a mission enemy (a Shooter going for the fan) washes the fan by that enemy's profile washPerHit.
//     The player's own water just splashes on the fan (no friendly fire).
//   - The bullet itself spawns its normal splash FX on contact, like a hit on an enemy.
//
// Unreal note: a UCapsuleComponent with a custom "FanHitbox" object channel that only blocks the projectile channel;
// OnComponentHit -> fan wash.

using JUTPS.WeaponSystem;
using UnityEngine;

namespace PIMLR.Missions.SaveTheFans
{
    [DisallowMultipleComponent]
    public class FanHitbox : MonoBehaviour
    {
        public const string LayerName = "FanHitbox";

        public FanHeat fan;

        public static FanHitbox Create(FanHeat fan)
        {
            var go = new GameObject("FanHitbox");
            go.transform.SetParent(fan.transform, false);
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogWarning("[SaveTheFans] Layer 'FanHitbox' is missing (Project Settings > Tags and Layers). Fan hitbox disabled.", fan);
                Object.Destroy(go);
                return null;
            }
            go.layer = layer;

            // Fit the capsule to the fan's body.
            float height = 1.8f;
            var renderers = fan.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
                height = Mathf.Clamp(b.max.y - fan.transform.position.y, 1.2f, 2.4f);
            }
            var cap = go.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.height = height;
            cap.radius = 0.32f;
            cap.center = new Vector3(0f, height * 0.5f, 0f);

            var hb = go.AddComponent<FanHitbox>();
            hb.fan = fan;
            return hb;
        }

        void OnCollisionEnter(Collision c)
        {
            if (!fan) return;
            var bullet = c.gameObject.GetComponent<Bullet>();
            if (!bullet) return;
            var owner = bullet.GetOwner();
            if (!owner) return;
            var brain = owner.GetComponent<FanAggroBrain>();
            if (!brain) return;                    // the player (or anyone else): splash only
            float wash = brain.WashPerHit;
            if (wash > 0f) fan.AddWash(wash);
        }
    }
}
