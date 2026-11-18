// PIMLR (Nov 16): enemies slid across the ground during their melee attacks. The robot punch clips (Zombie Left /
// Right / Strong Punch on "Animator Zombie Controller") have no root motion - the feet are planted - but the AI
// (ZombieAI / PatrolAI / PIMLR drivers) keeps feeding chase input and the Rigidbody keeps its run velocity while the
// full-body punch plays, so the capsule glides with frozen legs.
//
// While the Base Layer is in (or blending into) any punch / melee-attack state, this holds the same PimlrThrowLock the
// bomber throw uses: movement input zeroed after the AI writes it, horizontal Rigidbody velocity cleared after
// JUTPS's FixedUpdate. That alone still let the capsule creep ~1-2 m/s mid-punch (JUTPS's root-motion pass and
// capsule-vs-player depenetration write velocity after the lock clears it - measured Nov 16), so the swing is also
// HARD-planted: Rigidbody X/Z position frozen and the ground position pinned to where the attack started (fixed step
// and LateUpdate). Y is free (gravity, slopes) and turning on the spot still works, so they keep facing their target
// mid-swing. Everything is released the moment the attack starts blending back to locomotion.
//
// Installed on every AI character: by PimlrEnemySpawnDirector.Arrive (portal arrivals) and by a scene-load sweep for
// enemies placed directly in a scene. Never on the player.
//
// Unreal note: in the attack Anim Montage, an AnimNotifyState "PlantFeet" spanning the swing that sets the
// CharacterMovement's MaxWalkSpeed to 0 / calls StopMovementImmediately() on begin and restores on end - or simply
// enable root motion from the montage (RootMotionFromMontagesOnly), which overrides AI path-following velocity.

using JUTPS;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PimlrMeleeAttackPlant : MonoBehaviour
{
    // Base Layer attack states on the JUTPS character controllers the enemies use (short names).
    static readonly string[] AttackStateNames =
    {
        "Punch Attack 1", "Punch Attack 2", "Punch Attack 3",
        "Melee Attack One Hand 1", "Melee Attack One Hand 2", "Melee Attack One Hand 3", "Melee Attack One Hand 4", "Melee Attack One Hand 5",
        "Melee Attack Two Hand 1", "Melee Attack Two Hand 2", "Melee Attack Two Hand 3", "Melee Attack Two Hand 4", "Melee Attack Two Hand 5",
        "Melee Attack Dual Hand 1", "Melee Attack Dual Hand 2", "Melee Attack Dual Hand 3",
    };
    static int[] attackHashes;

    JUCharacterController character;
    Animator animator;
    PimlrThrowLock plantLock;
    Rigidbody body;
    RigidbodyConstraints savedConstraints;
    Vector3 anchor;
    bool planted;

    /// <summary>True while this enemy is planted in an attack (for debugging / tests).</summary>
    public bool IsPlanted => planted;

    public static void Attach(GameObject go)
    {
        if (!go || go.CompareTag("Player") || go.GetComponent<PimlrMeleeAttackPlant>()) return;
        if (!go.GetComponent<JUTPS.AI.JUCharacterArtificialInteligenceBrain>()) return; // AI characters only
        go.AddComponent<PimlrMeleeAttackPlant>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SweepScene();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => SweepScene();

    static void SweepScene()
    {
        foreach (var b in FindObjectsByType<JUTPS.AI.JUCharacterArtificialInteligenceBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b) Attach(b.gameObject);
    }

    void Awake()
    {
        character = GetComponent<JUCharacterController>();
        animator = GetComponent<Animator>();
        plantLock = PimlrThrowLock.For(this);
        body = GetComponent<Rigidbody>();
        if (attackHashes == null)
        {
            attackHashes = new int[AttackStateNames.Length];
            for (int i = 0; i < AttackStateNames.Length; i++) attackHashes[i] = Animator.StringToHash(AttackStateNames[i]);
        }
    }

    void OnDisable() => SetPlanted(false);

    void Update()
    {
        bool attack = character && animator && animator.isActiveAndEnabled &&
                      !character.IsDead && !character.IsRagdolled && !character.IsDriving && InAttack();
        SetPlanted(attack);
    }

    bool InAttack()
    {
        if (animator.IsInTransition(0))
        {
            // Planted while blending INTO an attack; free as soon as it blends back out to locomotion.
            return IsAttack(animator.GetNextAnimatorStateInfo(0).shortNameHash);
        }
        return IsAttack(animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
    }

    static bool IsAttack(int hash)
    {
        for (int i = 0; i < attackHashes.Length; i++) if (attackHashes[i] == hash) return true;
        return false;
    }

    void SetPlanted(bool on)
    {
        if (on == planted || !plantLock) { planted = on && plantLock; return; }
        planted = on;
        if (on)
        {
            plantLock.Hold(this);
            anchor = transform.position;
            if (body)
            {
                savedConstraints = body.constraints;
                Vector3 v = body.linearVelocity;
                body.linearVelocity = new Vector3(0f, v.y, 0f);
                body.constraints = savedConstraints | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
            }
        }
        else
        {
            plantLock.Release(this);
            if (body) body.constraints = savedConstraints;
        }
    }

    // Pin the ground position (X/Z) to where the swing started; height stays free.
    void Pin()
    {
        if (!planted) return;
        Vector3 p = transform.position;
        if ((p.x - anchor.x) * (p.x - anchor.x) + (p.z - anchor.z) * (p.z - anchor.z) < 0.0001f) return;
        p.x = anchor.x; p.z = anchor.z;
        if (body && !body.isKinematic) body.position = p;
        transform.position = p;
    }

    void FixedUpdate() => Pin();
    void LateUpdate() => Pin();
}
