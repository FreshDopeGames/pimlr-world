// PIMLR #STF (Save The Fans) — per-enemy-type targeting rules.
//
// DESIGN ANSWER to "does damaging an enemy pull it off the fan?":
//   It depends on the enemy type, and that's the point. One profile asset per type:
//     Thug    (PIMLR_Melee,  ZombieAI)  -> Grudge, low threshold  (a couple of hits pulls it onto you)
//     Shooter (PIMLR_Gunner, PatrolAI)  -> Grudge, high threshold (keeps washing from range unless you commit)
//     Bomber  (PIMLR_Bomber, PatrolAI)  -> SingleMinded           (never turns; its GrenadeThrower still lobs
//                                          balloons at the player on the way in, so it's the priority target)
//   And SaveTheFansMission.maxSimultaneousGrudges caps how many can be pulled at once,
//   so the player can never drag a whole wave away and leave the fan safe.
//
// Unreal note: this becomes a UPrimaryDataAsset (or a Data Table row) read by the
// enemy's Behavior Tree / StateTree. "Grudge" = a Blackboard bool + timer decorator.

using UnityEngine;

namespace PIMLR.Missions.SaveTheFans
{
    [CreateAssetMenu(fileName = "STF_Aggro_", menuName = "PIMLR/Save The Fans/Aggro Profile")]
    public class FanAggroProfile : ScriptableObject
    {
        public enum FocusMode
        {
            [Tooltip("Ignores the player completely and only ever goes for the fan.")]
            SingleMinded,
            [Tooltip("Goes for the fan, but switches to the player for a while after taking enough damage.")]
            Grudge
        }

        [Header("Focus")]
        public FocusMode focusMode = FocusMode.Grudge;

        [Tooltip("Fraction of max HP the enemy must lose inside the damage window before it turns on the player. 0.1 = 10%.")]
        [Range(0.01f, 1f)] public float grudgeDamageThreshold = 0.15f;

        [Tooltip("Seconds of damage history counted toward the threshold.")]
        [Min(0.25f)] public float damageWindow = 3f;

        [Tooltip("Seconds the enemy stays locked onto the player once provoked (refreshes if hit again).")]
        [Min(0.5f)] public float grudgeDuration = 6f;

        [Tooltip("If the player gets this far away, the grudge breaks and the enemy goes back to the fan.")]
        [Min(2f)] public float grudgeBreakDistance = 25f;

        // PIMLR #STF: how this enemy type gets to the fan.
        public enum Approach
        {
            [Tooltip("Walk straight to a spot beside the fan and wash it from there (thugs, shooters).")]
            Direct,
            [Tooltip("Circle the fan around grenade range, drifting in and out of it, and wash only with balloons (bombers).")]
            OrbitAtThrowRange
        }

        [Header("Movement")]
        public Approach approach = Approach.Direct;

        [Tooltip("Multiplies this enemy's move speed in Save The Fans (root-motion speed and JUTPS Speed). 1.2 = 20% faster.")]
        [Range(0.5f, 2f)] public float moveSpeedMultiplier = 1f;

        [Tooltip("Run to the fan instead of walking. Off = a slow, menacing approach.")]
        public bool runToFan = false;

        [Header("Orbit (OrbitAtThrowRange only)")]
        [Tooltip("Balloons are only thrown at the fan from within this distance. The orbit is centred on it.")]
        [Min(3f)] public float throwRangeToFan = 16f;
        [Tooltip("How far the orbit drifts inside and outside the throw range (metres either way).")]
        [Min(0f)] public float orbitSwing = 5f;
        [Tooltip("Seconds for one full in-and-out drift.")]
        [Min(1f)] public float orbitSwingPeriod = 10f;
        [Tooltip("How far ahead along the circle each walk target is, in degrees.")]
        [Range(5f, 90f)] public float orbitLeadDegrees = 30f;

        [Header("Washing the fan")]
        [Tooltip("Flat distance from the fan at which this enemy starts washing it.")]
        [Min(0.5f)] public float washRange = 2.5f;

        [Tooltip("Wash points per second applied to the fan while in range (fan max is 100 by default).")]
        [Min(0f)] public float washPerSecond = 6f;

        [Tooltip("Turn to face the fan while washing (reads better on camera).")]
        public bool faceFanWhileWashing = true;

        [Tooltip("Play the enemy's own attack (JUTPS DefaultUseOfAllItems) at the fan while washing. Good for melee thugs. Leave off for gunners/bombers.")]
        public bool swingAtFan = false;

        [Tooltip("Seconds between swings at the fan.")]
        [Min(0.3f)] public float swingInterval = 1.6f;

        [Tooltip("PIMLR #STF: swingers step in to this flat distance from the fan's centre so punches visibly land (JUTPS punch reach is about 1 m).")]
        [Range(0.5f, 2f)] public float meleeReach = 0.85f;

        // PIMLR #STF: gunners visibly fire water at the fan while washing it (the wash itself is still by range).
        [Tooltip("Fire the enemy's gun at the fan in bursts while washing it. For shooters.")]
        public bool shootAtFan = false;
        [Tooltip("Seconds of each burst.")]
        [Min(0.1f)] public float shootBurstSeconds = 0.8f;
        [Tooltip("Seconds from the start of one burst to the next.")]
        [Min(0.3f)] public float shootBurstInterval = 2.2f;
        [Tooltip("With Shoot At Fan on, the fan is washed only by water that actually hits it (this much per hit), not by standing in range.")]
        [Min(0f)] public float washPerHit = 2f;

        [Header("Score")]
        [Tooltip("Hype points toward the Fan Hype meter when this enemy is washed out.")]
        [Min(0)] public int hypeValue = 1;
    }
}
