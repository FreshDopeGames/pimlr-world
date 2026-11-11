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

        [Header("Score")]
        [Tooltip("Hype points toward the Fan Hype meter when this enemy is washed out.")]
        [Min(0)] public int hypeValue = 1;
    }
}
