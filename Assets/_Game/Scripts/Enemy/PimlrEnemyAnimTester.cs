using System.Collections;
using System.Collections.Generic;
using JUTPS;
using JUTPS.AI;
using JUTPS.PhysicsScripts;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PIMLR: animation test bench for enemy prefabs (made for the Enemy_Anims scene).
///
/// Drop enemy prefabs into the scene and press Play. In a scene named "Enemy_Anims" this sets itself up;
/// in any other scene, add it to an empty GameObject yourself.
///   - Every enemy gets the robot FX (red eyes + electrocute on death). Enemies placed by hand never go
///     through PimlrEnemySpawnDirector.Arrive, so before this they just ragdolled with no shock.
///   - Pick a state per enemy (or for all): AI, Idle, Walk, Run, Crouch, Jump, Roll, Punch, Aim, Fire, Throw...
///     Manual states switch the enemy's AI brains off and drive the JU character controller directly.
///   - Kill / Hit / Knockdown buttons. Dead enemies (killed any way, including typing Health 0 in the
///     Inspector) play their death + electrocute, then respawn fresh at their start spot in their last state.
///
/// Keys (Play Mode): Tab = select next enemy (or All), 1-9 / 0 / - / = = set state, K = kill (electrocute),
/// H = hit 25%, G = knockdown, R = respawn now, F = frame camera, ` = show/hide panel.
/// Look & feel of the shock: Assets/_Game/Resources/PimlrRobotFX.asset.
/// </summary>
[DefaultExecutionOrder(-500)]
public class PimlrEnemyAnimTester : MonoBehaviour
{
    public enum TestState { AI, Idle, Walk, Run, Crouch, CrouchWalk, Jump, Roll, Punch, MeleeAttack, Aim, AimFire, Throw }

    const string AutoSceneName = "Enemy_Anims";

    // MonoBehaviours that drive an enemy on their own. Switched off for every state except AI.
    static readonly string[] BrainTypeNames =
    {
        "GrenadeThrower", "PimlrBomberAI", "PimlrGunnerAI", "PimlrHuntPlayer", "AIRandomDestination",
        "EnemyCombatAI", "EnemyNavmeshLeash", "PimlrSeekIntersection",
    };

    [Header("Startup")]
    [Tooltip("State every enemy starts in.")]
    public TestState startState = TestState.Idle;

    [Header("Respawn")]
    public bool autoRespawn = true;
    [Tooltip("Seconds to keep the body on the ground after the electrocute finishes, before respawning.")]
    public float corpseLinger = 1.5f;

    [Header("Movement states")]
    [Tooltip("Walk / Run / Crouch Walk pace back and forth this far in front of the start spot.")]
    public float paceDistance = 5f;

    [Header("Repeating actions (seconds)")]
    public float jumpInterval = 2.5f;
    public float rollInterval = 2.5f;
    public float punchInterval = 1.2f;
    public float throwInterval = 3f;
    public string throwTrigger = "Throw";

    [Header("UI")]
    public bool showPanel = true;

    class Enemy
    {
        public string name;
        public GameObject go;
        public GameObject template;      // prefab asset (Editor) or a hidden clone (builds)
        public bool templateIsClone;
        public Vector3 homePos;
        public Quaternion homeRot;
        public TestState state;
        public JUCharacterController ctrl;
        public JUHealth health;
        public AdvancedRagdollController ragdoll;
        public RobotShockEffect shock;
        public Animator anim;
        public readonly List<Behaviour> brains = new List<Behaviour>();
        public bool dying;
        public Coroutine pending;
        public float nextAction;
        public bool paceOut = true;
    }

    readonly List<Enemy> enemies = new List<Enemy>();
    Transform templateRoot;
    int selected = -1; // -1 = all
    Vector2 scroll;
    Rect windowRect = new Rect(10, 10, 430, 520);

    // ---------------------------------------------------------------- auto setup in Enemy_Anims

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAutoCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAutoCreate(scene);

    static void TryAutoCreate(Scene scene)
    {
        if (scene.name != AutoSceneName) return;
        if (FindObjectsByType<PimlrEnemyAnimTester>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0) return;
        var go = new GameObject("PIMLR_EnemyAnimTester");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<PimlrEnemyAnimTester>();
    }

    // ---------------------------------------------------------------- setup

    void Start()
    {
        var holder = new GameObject("PIMLR_EnemyTemplates");
        holder.SetActive(false); // clones parked here never wake up
        templateRoot = holder.transform;
        templateRoot.SetParent(transform, false);

        foreach (var c in FindObjectsByType<JUCharacterController>(FindObjectsSortMode.None))
        {
            if (!c || c.CompareTag("Player") || c.transform.root != c.transform) continue;
            if (!c.IsArtificialIntelligence && !c.GetComponent<JUCharacterArtificialInteligenceBrain>()) continue;
            if (!c.GetComponent<JUHealth>()) continue;

            var e = new Enemy
            {
                name = c.gameObject.name,
                homePos = c.transform.position,
                homeRot = c.transform.rotation,
                state = startState,
            };
            MakeTemplate(e, c.gameObject);
            Bind(e, c.gameObject);
            enemies.Add(e);
            ApplyState(e, startState, true);
        }

        enemies.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        Debug.Log("PIMLR Enemy Anim Tester: " + enemies.Count + " enemies ready. Tab/1-9/K/H/G/R/F, ` toggles the panel.", this);
    }

    void MakeTemplate(Enemy e, GameObject sceneObj)
    {
#if UNITY_EDITOR
        // Respawn from the prefab itself: a truly fresh enemy (health, AI, damagers, ragdoll, layers).
        var src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(sceneObj);
        if (src && UnityEditor.PrefabUtility.IsOutermostPrefabInstanceRoot(sceneObj))
        {
            e.template = src;
            return;
        }
#endif
        // Not a prefab instance (or a build): clone it now, while it's alive, and park the clone switched off.
        e.template = Instantiate(sceneObj, templateRoot);
        e.template.name = e.name + " (template)";
        e.templateIsClone = true;
    }

    void Bind(Enemy e, GameObject go)
    {
        e.go = go;
        e.go.name = e.name;
        e.ctrl = go.GetComponent<JUCharacterController>();
        e.health = go.GetComponent<JUHealth>();
        e.ragdoll = go.GetComponent<AdvancedRagdollController>();
        e.anim = go.GetComponent<Animator>();
        e.dying = false;
        e.paceOut = true;
        e.nextAction = Time.time + 0.5f;

        // Robot eyes + electrocute death. Same call the real spawners make in PimlrEnemySpawnDirector.Arrive.
        PimlrRobotFX.Attach(go);
        e.shock = go.GetComponent<RobotShockEffect>();

        e.brains.Clear();
        foreach (var b in go.GetComponents<Behaviour>())
        {
            if (!b || !b.enabled) continue;
            if (b is JUCharacterArtificialInteligenceBrain || System.Array.IndexOf(BrainTypeNames, b.GetType().Name) >= 0)
                e.brains.Add(b);
        }
    }

    // ---------------------------------------------------------------- states

    void ApplyState(Enemy e, TestState s, bool force = false)
    {
        if (!force && e.state == s) return;
        e.state = s;
        e.nextAction = Time.time + 0.3f;
        e.paceOut = true;
        if (!Alive(e)) return; // picked up again on respawn

        ResetControls(e);
        bool ai = s == TestState.AI;
        foreach (var b in e.brains) if (b) b.enabled = ai;
    }

    static void ResetControls(Enemy e)
    {
        var c = e.ctrl;
        if (!c) return;
        c._Move(0, 0, false);
        c._GetUp();
        c.FiringMode = false;
        c.FiringModeIK = false;
        c.IsAiming = false;
    }

    static bool Alive(Enemy e) => e.go && e.health && !e.health.IsDead && e.ctrl;

    void Update()
    {
        HandleKeys();

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!e.go) { if (autoRespawn && !e.dying) e.pending = StartCoroutine(Respawn(e, 0f)); continue; }
            if (e.health && e.health.IsDead)
            {
                if (!e.dying && autoRespawn) e.pending = StartCoroutine(DeathThenRespawn(e));
                continue;
            }
            if (e.state != TestState.AI) Drive(e);
        }
    }

    void Drive(Enemy e)
    {
        var c = e.ctrl;
        if (!c || c.IsRagdolled) return;

        Vector3 fwd = e.homeRot * Vector3.forward;
        Vector3 aimPoint = e.homePos + fwd * 10f + Vector3.up * 1.4f;
        bool due = Time.time >= e.nextAction;

        switch (e.state)
        {
            case TestState.Idle:
                c._Move(0, 0, false);
                break;

            case TestState.Walk: Pace(e, false); break;
            case TestState.Run: Pace(e, true); break;

            case TestState.Crouch:
                c._Move(0, 0, false);
                c._Crouch();
                break;

            case TestState.CrouchWalk:
                c._Crouch();
                Pace(e, false);
                break;

            case TestState.Jump:
                c._Move(0, 0, false);
                if (due) { c._Jump(); e.nextAction = Time.time + jumpInterval; }
                break;

            case TestState.Roll:
                c._Move(0, 0, false);
                if (due) { c._Roll(); e.nextAction = Time.time + rollInterval; }
                break;

            case TestState.Punch:
                c._Move(0, 0, false);
                if (due) { c._DoPunch(); e.nextAction = Time.time + punchInterval; }
                break;

            case TestState.MeleeAttack:
                // Same call ZombieAI makes when it attacks (uses a melee weapon if one is held, else punches).
                c._Move(0, 0, false);
                if (due)
                {
                    c.DefaultUseOfAllItems(true, true, false, false, false, true);
                    e.nextAction = Time.time + punchInterval;
                }
                break;

            case TestState.Aim:
            case TestState.AimFire:
                c._Move(0, 0, false);
                c.LookAtPosition = aimPoint;
                c.DoLookAt(aimPoint);
                c.FiringMode = true;
                c.FiringModeIK = true;
                bool fire = e.state == TestState.AimFire;
                c.DefaultUseOfAllItems(fire, fire, false, true, false); // same call PatrolAI makes
                break;

            case TestState.Throw:
                c._Move(0, 0, false);
                if (due)
                {
                    if (e.anim && HasTrigger(e.anim, throwTrigger)) e.anim.SetTrigger(throwTrigger);
                    e.nextAction = Time.time + throwInterval;
                }
                break;
        }
    }

    void Pace(Enemy e, bool run)
    {
        Vector3 fwd = e.homeRot * Vector3.forward;
        Vector3 target = e.paceOut ? e.homePos + fwd * paceDistance : e.homePos;
        Vector3 to = target - e.go.transform.position;
        to.y = 0f;
        if (to.magnitude < 0.5f) { e.paceOut = !e.paceOut; return; }
        to.Normalize();
        e.ctrl._Move(to.x, to.z, run); // world-space input, like JU's FollowPath
    }

    static bool HasTrigger(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }

    // ---------------------------------------------------------------- death / respawn

    IEnumerator DeathThenRespawn(Enemy e)
    {
        e.dying = true;
        float died = Time.time;

        // Let the death play out: RobotShockEffect starts ~0.25 s after death and runs shockDuration.
        float shockLen = PimlrRobotFX.Settings ? PimlrRobotFX.Settings.shockDuration : 2.5f;
        float cap = died + shockLen + 3f;
        yield return new WaitForSeconds(0.4f);
        while (Time.time < cap && e.go && e.shock && e.shock.IsShocking) yield return null;
        float minEnd = died + shockLen + 0.3f; // death-anim enemies (no ragdoll) still get the full beat
        if (Time.time < minEnd) yield return new WaitForSeconds(minEnd - Time.time);

        yield return Respawn(e, corpseLinger);
    }

    IEnumerator Respawn(Enemy e, float delay)
    {
        e.dying = true;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        RemoveBody(e);

        if (!e.template)
        {
            Debug.LogWarning("PIMLR Enemy Anim Tester: no template for " + e.name + ", can't respawn.", this);
            yield break;
        }

        GameObject fresh = Instantiate(e.template, e.homePos, e.homeRot);
        if (!fresh.activeSelf) fresh.SetActive(true);

        e.pending = null;
        Bind(e, fresh);
        ApplyState(e, e.state, true);
    }

    void RemoveBody(Enemy e)
    {
        if (!e.go) return;
        // A ragdolled JU character has its Hips unparented to the scene root - take them with the body.
        if (e.ragdoll && e.ragdoll.Hips && e.ragdoll.Hips.root != e.go.transform) Destroy(e.ragdoll.Hips.gameObject);
        Destroy(e.go);
        e.go = null;
    }

    // ---------------------------------------------------------------- actions

    void Kill(Enemy e)
    {
        if (!Alive(e)) return;
        e.health.DoDamage(e.health.Health + 1000f); // real damage path -> JU death -> electrocute
    }

    void Hit(Enemy e)
    {
        if (!Alive(e)) return;
        Vector3 at = e.go.transform.position + Vector3.up * 1.2f;
        if (e.anim && e.anim.isHuman)
        {
            var chest = e.anim.GetBoneTransform(HumanBodyBones.Chest);
            if (chest) at = chest.position;
        }
        e.health.DoDamage(e.health.MaxHealth * 0.25f, at);
    }

    void Knockdown(Enemy e)
    {
        if (!Alive(e) || !e.ragdoll) return;
        e.ragdoll.Fall(); // JU ragdoll, then its own get-up animation
    }

    void RespawnNow(Enemy e)
    {
        if (e.pending != null) StopCoroutine(e.pending); // cancel a death countdown already running
        e.pending = StartCoroutine(Respawn(e, 0f));
    }

    void FrameCamera(Enemy e)
    {
        var cam = Camera.main;
        if (!cam || !e.go) return;
        Vector3 p = e.go.transform.position;
        Vector3 fwd = e.go.transform.forward;
        cam.transform.position = p + fwd * 4f + Vector3.up * 1.8f;
        cam.transform.LookAt(p + Vector3.up * 1f);
    }

    IEnumerable<Enemy> Targets()
    {
        if (selected >= 0 && selected < enemies.Count) yield return enemies[selected];
        else foreach (var e in enemies) yield return e;
    }

    // ---------------------------------------------------------------- keys

    static readonly TestState[] KeyStates =
    {
        TestState.AI, TestState.Idle, TestState.Walk, TestState.Run, TestState.Crouch, TestState.CrouchWalk,
        TestState.Jump, TestState.Roll, TestState.Punch, TestState.Aim, TestState.AimFire, TestState.Throw,
    };

    void HandleKeys()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.backquoteKey.wasPressedThisFrame) showPanel = !showPanel;
        if (kb.tabKey.wasPressedThisFrame) selected = selected + 1 >= enemies.Count ? -1 : selected + 1;

        Key[] stateKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6,
            Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0, Key.Minus, Key.Equals,
        };
        for (int i = 0; i < stateKeys.Length; i++)
            if (kb[stateKeys[i]].wasPressedThisFrame)
                foreach (var e in Targets()) ApplyState(e, KeyStates[i]);

        if (kb.kKey.wasPressedThisFrame) foreach (var e in Targets()) Kill(e);
        if (kb.hKey.wasPressedThisFrame) foreach (var e in Targets()) Hit(e);
        if (kb.gKey.wasPressedThisFrame) foreach (var e in Targets()) Knockdown(e);
        if (kb.rKey.wasPressedThisFrame) foreach (var e in new List<Enemy>(Targets())) RespawnNow(e);
        if (kb.fKey.wasPressedThisFrame && selected >= 0 && selected < enemies.Count) FrameCamera(enemies[selected]);
#endif
    }

    // ---------------------------------------------------------------- panel

    void OnGUI()
    {
        if (!showPanel) return;
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "PIMLR Enemy Anim Tester");
    }

    void DrawWindow(int id)
    {
        GUILayout.Label("Tab select | 1-9,0,-,= state | K kill | H hit | G knockdown | R respawn | F frame | ` hide");

        GUILayout.BeginHorizontal();
        autoRespawn = GUILayout.Toggle(autoRespawn, "Auto respawn");
        GUILayout.Label("Linger " + corpseLinger.ToString("0.0") + "s", GUILayout.Width(80));
        corpseLinger = GUILayout.HorizontalSlider(corpseLinger, 0f, 5f, GUILayout.Width(100));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label(selected < 0 ? "Target: ALL enemies" : "Target: " + enemies[selected].name);
        int perRow = 4;
        for (int i = 0; i < KeyStates.Length; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, KeyStates.Length); j++)
                if (GUILayout.Button(KeyStates[j].ToString()))
                    foreach (var e in Targets()) ApplyState(e, KeyStates[j]);
            GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Melee Atk")) foreach (var e in Targets()) ApplyState(e, TestState.MeleeAttack);
        if (GUILayout.Button("Kill (zap)")) foreach (var e in Targets()) Kill(e);
        if (GUILayout.Button("Hit 25%")) foreach (var e in Targets()) Hit(e);
        if (GUILayout.Button("Knockdown")) foreach (var e in Targets()) Knockdown(e);
        if (GUILayout.Button("Respawn")) foreach (var e in new List<Enemy>(Targets())) RespawnNow(e);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        scroll = GUILayout.BeginScrollView(scroll);
        if (GUILayout.Toggle(selected < 0, " ALL")) selected = -1;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            string status = !e.go ? "respawning" : e.health && e.health.IsDead ? "DEAD" :
                e.health ? Mathf.CeilToInt(e.health.Health) + "/" + Mathf.CeilToInt(e.health.MaxHealth) + " hp" : "?";
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selected == i, " " + e.name, GUILayout.Width(170))) selected = i;
            else if (selected == i) selected = -1;
            GUILayout.Label(e.state + " | " + status);
            if (GUILayout.Button("F", GUILayout.Width(24))) FrameCamera(e);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
}
