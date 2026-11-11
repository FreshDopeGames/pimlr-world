// PIMLR #STF (Save The Fans) — editor tooling. Window: PIMLR > Save The Fans > Tools.
// Written for Unity 6000.6 (unityv6 branch). IMGUI EditorWindow, no UXML factories.
//
//   Setup       one-click mission manager, default data assets (with the real enemy prefabs
//               assigned), turn NPCs into fans, copy Sirihanna's ring onto them as a golden ring.
//   Spawns      auto-place enemy spawn points around a fan that are ON the NavMesh, have a
//               complete path to the fan, sit at a sane distance and (ideally) are hidden
//               from the fan's eye line so portals don't open in plain view.
//   Validate    checks every heat, and the travel between fans against each timer using real
//               NavMesh route lengths, walking speed and the nearest bike.
//
// Scene lookups walk the open scene's hierarchy instead of Object.FindObjectsByType /
// FindFirstObjectByType: in Unity 6.6 the sort-mode overloads and FindFirstObjectByType are
// deprecated, and the hierarchy walk behaves the same in every version.
//
// Must live in an Editor folder. No asmdef: it needs Assembly-CSharp types.
//
// Unreal note: an Editor Utility Widget. NavMesh checks = UNavigationSystemV1::FindPathToLocationSynchronously;
// spawn scoring = an EQS query (Points: Donut around fan, Tests: Pathfinding, Trace, Distance).

using System.Collections.Generic;
using System.Linq;
using JUTPS;
using JUTPS.AI;
using JUTPS.VehicleSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace PIMLR.Missions.SaveTheFans.EditorTools
{
    public class SaveTheFansTools : EditorWindow
    {
        const string DataFolder = "Assets/_Game/SaveTheFans/Data";
        const string EnemyFolder = "Assets/_Game/Prefabs/Enemies";
        static readonly string[] DefaultFanNames = { "PIMLR_Jose", "PIMLR_Tiana", "PIMLR_Willie" };
        const string SirihannaRingName = "GlowCircle";

        // spawn placement settings
        float minRadius = 18f, maxRadius = 35f;
        int rings = 3, samplesPerRing = 16, spawnCount = 6;
        float maxPathLength = 70f, minSeparation = 6f;
        bool preferHiddenFromFan = true;
        LayerMask occlusionMask = 1; // buildings are on Default in YannicksWorld

        // validation settings (JUCharacterController.Speed is 3; the Movement Speed track makes it 6)
        float walkSpeed = 3f, sprintSpeed = 5f, bikeSpeed = 12f;
        float minFanSeparation = 60f;

        Vector2 scroll;
        readonly List<(MessageType type, string msg, Object ctx)> report = new List<(MessageType, string, Object)>();

        [MenuItem("PIMLR/Save The Fans/Tools")]
        static void Open() => GetWindow<SaveTheFansTools>("Save The Fans");

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            Section("1. Setup");
            if (GUILayout.Button("Create Mission Manager in Scene")) CreateMissionManager();
            if (GUILayout.Button("Create Default Data Assets (3 aggro profiles + 3 wave sets)")) CreateDefaultData();
            if (GUILayout.Button("Set Up Jose, Tiana and Willie as Fans")) MakeFans(FindDefaultFans());
            if (GUILayout.Button("Make Selected NPCs Fans")) MakeFans(Selection.gameObjects);
            if (GUILayout.Button("Give Every Fan a Golden Ring (copies Sirihanna's GlowCircle)")) CopyRingOntoFans();

            Section("2. Spawn points");
            minRadius = EditorGUILayout.FloatField("Min radius (m)", minRadius);
            maxRadius = EditorGUILayout.FloatField("Max radius (m)", maxRadius);
            rings = EditorGUILayout.IntSlider("Rings", rings, 1, 6);
            samplesPerRing = EditorGUILayout.IntSlider("Samples per ring", samplesPerRing, 6, 48);
            spawnCount = EditorGUILayout.IntSlider("Spawn points per fan", spawnCount, 1, 16);
            maxPathLength = EditorGUILayout.FloatField("Max walk path to fan (m)", maxPathLength);
            minSeparation = EditorGUILayout.FloatField("Min spacing between points (m)", minSeparation);
            preferHiddenFromFan = EditorGUILayout.Toggle("Prefer hidden from fan", preferHiddenFromFan);
            occlusionMask = LayerMaskField("Occlusion layers", occlusionMask);
            if (GUILayout.Button("Auto-Place Spawn Points for Selected Fan(s)")) AutoPlace(Selection.gameObjects.Select(g => g.GetComponent<FanHeat>()).Where(f => f != null));
            if (GUILayout.Button("Auto-Place Spawn Points for ALL Fans")) AutoPlace(FindInScene<FanHeat>());

            Section("3. Validate");
            walkSpeed = EditorGUILayout.FloatField("Player walk speed (m/s)", walkSpeed);
            sprintSpeed = EditorGUILayout.FloatField("Player sprint speed (m/s)", sprintSpeed);
            bikeSpeed = EditorGUILayout.FloatField("Bike cruising speed (m/s)", bikeSpeed);
            minFanSeparation = EditorGUILayout.FloatField("Min distance between fans (m)", minFanSeparation);
            if (GUILayout.Button("Validate All Heats")) Validate();
            foreach (var (type, msg, ctx) in report)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(msg, type);
                if (ctx && GUILayout.Button("Select", GUILayout.Width(55), GUILayout.Height(38))) { Selection.activeObject = ctx; EditorGUIUtility.PingObject(ctx); }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        static void Section(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        static LayerMask LayerMaskField(string label, LayerMask mask)
        {
            var names = UnityEditorInternal.InternalEditorUtility.layers;
            int shown = 0;
            for (int i = 0; i < names.Length; i++)
                if ((mask.value & (1 << LayerMask.NameToLayer(names[i]))) != 0) shown |= 1 << i;
            shown = EditorGUILayout.MaskField(label, shown, names);
            int result = 0;
            for (int i = 0; i < names.Length; i++)
                if ((shown & (1 << i)) != 0) result |= 1 << LayerMask.NameToLayer(names[i]);
            return result;
        }

        // ================================================================ scene helpers
        static List<T> FindInScene<T>() where T : Component
        {
            var list = new List<T>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return list;
        }

        static GameObject FindObjectNamed(string objectName)
        {
            foreach (var t in FindInScene<Transform>())
                if (t.name.Trim() == objectName) return t.gameObject;
            return null;
        }

        static GameObject[] FindDefaultFans()
        {
            var found = new List<GameObject>();
            foreach (var n in DefaultFanNames)
            {
                var go = FindObjectNamed(n);
                if (go) found.Add(go);
                else Debug.LogWarning($"[SaveTheFans] '{n}' not found in the open scene.");
            }
            return found.ToArray();
        }

        static SaveTheFansMission FindMission() => FindInScene<SaveTheFansMission>().FirstOrDefault();

        // ================================================================ setup
        static void CreateMissionManager()
        {
            var existing = FindMission();
            if (existing) { Selection.activeObject = existing; Debug.Log("[SaveTheFans] Mission manager already exists."); return; }

            var go = new GameObject("SaveTheFans_Mission");
            Undo.RegisterCreatedObjectUndo(go, "Create Save The Fans Mission");
            var mission = go.AddComponent<SaveTheFansMission>();
            mission.cameraDirector = go.AddComponent<MissionCameraDirector>();

            var enemies = new GameObject("STF_Enemies");
            enemies.transform.SetParent(go.transform, false);
            mission.enemiesParent = enemies.transform;

            foreach (var fan in FindInScene<FanHeat>()) mission.heats.Add(fan);
            mission.defaultAggroProfile = AssetDatabase.LoadAssetAtPath<FanAggroProfile>($"{DataFolder}/STF_Aggro_Thug.asset");

            Selection.activeObject = go;
            EditorUtility.SetDirty(mission);
        }

        static void CreateDefaultData()
        {
            EnsureFolder(DataFolder);

            var melee = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyFolder}/PIMLR_Melee.prefab");
            var gunner = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyFolder}/PIMLR_Gunner.prefab");
            var bomber = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyFolder}/PIMLR_Bomber.prefab");
            if (!melee || !gunner || !bomber)
                Debug.LogWarning($"[SaveTheFans] Couldn't find all of PIMLR_Melee / PIMLR_Gunner / PIMLR_Bomber in {EnemyFolder}. Assign the missing prefabs in the wave sets by hand.");

            // focus mode, damage share to provoke, grudge seconds, wash range, wash/sec, hype, swings at the fan
            var thug = MakeProfile("STF_Aggro_Thug", FanAggroProfile.FocusMode.Grudge, 0.12f, 6f, 2.2f, 3f, 1, true);
            var shooter = MakeProfile("STF_Aggro_Shooter", FanAggroProfile.FocusMode.Grudge, 0.35f, 5f, 9f, 2.5f, 2, false);
            var bomb = MakeProfile("STF_Aggro_Bomber", FanAggroProfile.FocusMode.SingleMinded, 1f, 1f, 4f, 8f, 3, false);

            // per wave: { thugs, shooters, bombers }
            MakeWaveSet("STF_Waves_Fan1", 100, 1f, new[] { new[] { 3, 0, 0 }, new[] { 5, 0, 0 }, new[] { 7, 0, 0 } }, thug, shooter, bomb, melee, gunner, bomber);
            MakeWaveSet("STF_Waves_Fan2", 150, 1.15f, new[] { new[] { 4, 0, 0 }, new[] { 4, 2, 0 }, new[] { 6, 3, 0 }, new[] { 8, 4, 0 } }, thug, shooter, bomb, melee, gunner, bomber);
            MakeWaveSet("STF_Waves_Fan3", 250, 1.3f, new[] { new[] { 5, 2, 0 }, new[] { 6, 3, 1 }, new[] { 8, 3, 1 }, new[] { 9, 4, 2 }, new[] { 10, 5, 3 } }, thug, shooter, bomb, melee, gunner, bomber);

            var mission = FindMission();
            if (mission && !mission.defaultAggroProfile) { Undo.RecordObject(mission, "Assign default aggro"); mission.defaultAggroProfile = thug; EditorUtility.SetDirty(mission); }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SaveTheFans] Default data created in {DataFolder}.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(DataFolder);
        }

        static FanAggroProfile MakeProfile(string assetName, FanAggroProfile.FocusMode mode, float threshold, float duration, float range, float wash, int hype, bool swing)
        {
            string path = $"{DataFolder}/{assetName}.asset";
            var p = AssetDatabase.LoadAssetAtPath<FanAggroProfile>(path);
            if (p) return p;
            p = CreateInstance<FanAggroProfile>();
            p.focusMode = mode;
            p.grudgeDamageThreshold = threshold;
            p.grudgeDuration = duration;
            p.washRange = range;
            p.washPerSecond = wash;
            p.hypeValue = hype;
            p.swingAtFan = swing;
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        static void MakeWaveSet(string assetName, int coins, float washMult, int[][] counts,
            FanAggroProfile thug, FanAggroProfile shooter, FanAggroProfile bomb, GameObject melee, GameObject gunner, GameObject bomber)
        {
            string path = $"{DataFolder}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<FanWaveSet>(path)) return;
            var set = CreateInstance<FanWaveSet>();
            set.coinReward = coins;
            set.washRateMultiplier = washMult;
            for (int w = 0; w < counts.Length; w++)
            {
                var wave = new FanWave { name = $"Wave {w + 1}", preWaveDelay = w == 0 ? 0f : 5f, maxAlive = 4 + w };
                if (counts[w][0] > 0) wave.entries.Add(new FanSpawnEntry { label = "Thug", prefab = melee, count = counts[w][0], aggroProfile = thug });
                if (counts[w][1] > 0) wave.entries.Add(new FanSpawnEntry { label = "Shooter", prefab = gunner, count = counts[w][1], aggroProfile = shooter });
                if (counts[w][2] > 0) wave.entries.Add(new FanSpawnEntry { label = "Bomber", prefab = bomber, count = counts[w][2], aggroProfile = bomb });
                set.waves.Add(wave);
            }
            AssetDatabase.CreateAsset(set, path);
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static void MakeFans(IEnumerable<GameObject> objects)
        {
            var mission = FindMission();
            int index = 0, made = 0;
            foreach (var go in objects)
            {
                if (!go || EditorUtility.IsPersistent(go)) continue; // scene objects only
                var fan = go.GetComponent<FanHeat>();
                if (!fan) fan = Undo.AddComponent<FanHeat>(go);
                Undo.RecordObject(fan, "Set up fan");
                if (string.IsNullOrEmpty(fan.fanDisplayName) || fan.fanDisplayName == "Fan")
                    fan.fanDisplayName = go.name.Replace("PIMLR_", "").Trim();

                var blip = go.GetComponent<FanMinimapBlip>();
                if (!blip) blip = Undo.AddComponent<FanMinimapBlip>(go);
                fan.minimapBlip = blip;
                if (!fan.animator) fan.animator = go.GetComponentInChildren<Animator>();

                if (!fan.waveSet)
                    fan.waveSet = AssetDatabase.LoadAssetAtPath<FanWaveSet>($"{DataFolder}/STF_Waves_Fan{Mathf.Min(3, ++index)}.asset");

                if (!go.transform.Find("STF_SpawnPoints"))
                {
                    var sp = new GameObject("STF_SpawnPoints");
                    Undo.RegisterCreatedObjectUndo(sp, "Create spawn root");
                    sp.transform.SetParent(go.transform, false);
                }

                if (mission && !mission.heats.Contains(fan))
                {
                    Undo.RecordObject(mission, "Add heat");
                    mission.heats.Add(fan);
                    EditorUtility.SetDirty(mission);
                }
                EditorUtility.SetDirty(fan);
                made++;
            }
            Debug.Log($"[SaveTheFans] {made} fan(s) set up. Check the order of the mission's Heats list, then place spawn points.");
        }

        static void CopyRingOntoFans()
        {
            var source = FindObjectNamed(SirihannaRingName);
            if (!source && Selection.activeGameObject && !Selection.activeGameObject.GetComponent<FanHeat>()) source = Selection.activeGameObject;
            if (!source)
            {
                Debug.LogError($"[SaveTheFans] Couldn't find '{SirihannaRingName}' (under 'AI Chat Interaction') in the open scene. Select the ring object and press the button again.");
                return;
            }

            // Sirihanna's ring sits this far above the ground she stands on.
            float heightAboveGround = 0.02f;
            if (Physics.Raycast(source.transform.position + Vector3.up * 2f, Vector3.down, out var srcHit, 6f, 1, QueryTriggerInteraction.Ignore))
                heightAboveGround = Mathf.Clamp(source.transform.position.y - srcHit.point.y, 0f, 0.5f);

            int made = 0;
            foreach (var fan in FindInScene<FanHeat>())
            {
                if (fan.ring) Undo.DestroyObjectImmediate(fan.ring.gameObject);

                var ring = Instantiate(source, fan.transform, true); // keep world rotation/scale
                Undo.RegisterCreatedObjectUndo(ring, "Copy ring");
                ring.name = "STF_GoldenRing";
                ring.SetActive(true);

                Vector3 feet = fan.transform.position;
                if (Physics.Raycast(feet + Vector3.up * 2f, Vector3.down, out var hit, 6f, 1, QueryTriggerInteraction.Ignore)) feet = hit.point;
                ring.transform.position = feet + Vector3.up * heightAboveGround;

                // Visuals only: no scripts or colliders from Sirihanna's setup.
                foreach (var mb in ring.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb && !(mb is FanGlowRing)) DestroyImmediate(mb);
                foreach (var col in ring.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);

                var glow = ring.GetComponent<FanGlowRing>();
                if (!glow) glow = ring.AddComponent<FanGlowRing>();
                Undo.RecordObject(fan, "Assign ring");
                fan.ring = glow;
                EditorUtility.SetDirty(fan);
                made++;
            }
            Debug.Log($"[SaveTheFans] Golden ring added to {made} fan(s), copied from '{source.name}'.");
        }

        // ================================================================ spawn placement
        void AutoPlace(IEnumerable<FanHeat> fans)
        {
            var allFans = FindInScene<FanHeat>();
            foreach (var fan in fans) AutoPlace(fan, allFans);
        }

        void AutoPlace(FanHeat fan, List<FanHeat> allFans)
        {
            Vector3 fanPos = fan.transform.position;
            if (!NavMesh.SamplePosition(fanPos, out var fanHit, 2f, NavMesh.AllAreas))
            {
                Debug.LogError($"[SaveTheFans] {fan.name} isn't on the NavMesh (within 2 m). Rebuild the NavMesh first (PimlrNavMeshRebuilder).", fan);
                return;
            }

            Vector3 eye = fan.FocusPoint;
            var hidden = new List<Vector3>();
            var visible = new List<Vector3>();
            var path = new NavMeshPath();

            for (int r = 0; r < rings; r++)
            {
                float radius = rings == 1 ? (minRadius + maxRadius) * 0.5f : Mathf.Lerp(minRadius, maxRadius, r / (float)(rings - 1));
                for (int s = 0; s < samplesPerRing; s++)
                {
                    float ang = (s + (r % 2) * 0.5f) / samplesPerRing * Mathf.PI * 2f;
                    Vector3 p = fanPos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
                    if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(hit.position.y - fanHit.position.y) > 4f) continue; // rooftops / overpasses
                    if (!NavMesh.CalculatePath(hit.position, fanHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;

                    float len = PathLength(path);
                    if (len > maxPathLength || len < minRadius * 0.8f) continue;

                    // Not on top of another fan (enemies would walk straight through that fan's ring).
                    bool nearOtherFan = allFans.Any(f => f && f != fan && Vector3.Distance(f.transform.position, hit.position) < f.arrivalRadius);
                    if (nearOtherFan) continue;

                    bool blocked = Physics.Linecast(eye, hit.position + Vector3.up * 1.2f, occlusionMask, QueryTriggerInteraction.Ignore);
                    (blocked ? hidden : visible).Add(hit.position);
                }
            }

            var chosen = new List<Vector3>();
            void Take(List<Vector3> src)
            {
                foreach (var c in src.OrderBy(_ => Random.value))
                {
                    if (chosen.Count >= spawnCount) return;
                    if (chosen.All(x => Vector3.Distance(x, c) >= minSeparation)) chosen.Add(c);
                }
            }
            if (preferHiddenFromFan) { Take(hidden); Take(visible); }
            else Take(hidden.Concat(visible).ToList());

            if (chosen.Count == 0)
            {
                Debug.LogError($"[SaveTheFans] No valid spawn points found around {fan.name}. Widen the radius or max path length, or check the NavMesh.", fan);
                return;
            }

            var root = fan.transform.Find("STF_SpawnPoints");
            if (!root)
            {
                root = new GameObject("STF_SpawnPoints").transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Spawn root");
                root.SetParent(fan.transform, false);
            }
            for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            var list = new List<Transform>();
            for (int i = 0; i < chosen.Count; i++)
            {
                var sp = new GameObject($"Spawn_{i + 1:00}").transform;
                Undo.RegisterCreatedObjectUndo(sp.gameObject, "Spawn point");
                sp.SetParent(root, true);
                sp.position = chosen[i];
                Vector3 look = fanPos - chosen[i]; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) sp.rotation = Quaternion.LookRotation(look);
                list.Add(sp);
            }

            Undo.RecordObject(fan, "Assign spawn points");
            fan.spawnPoints = list.ToArray();
            EditorUtility.SetDirty(fan);
            Debug.Log($"[SaveTheFans] {fan.name}: placed {chosen.Count} spawn points ({hidden.Count} hidden / {visible.Count} visible candidates).", fan);
        }

        static float PathLength(NavMeshPath p)
        {
            float len = 0f;
            for (int i = 1; i < p.corners.Length; i++) len += Vector3.Distance(p.corners[i - 1], p.corners[i]);
            return len;
        }

        // ================================================================ validation
        void Validate()
        {
            report.Clear();
            var mission = FindMission();
            if (!mission) { Add(MessageType.Error, "No SaveTheFansMission in the open scene. Use 'Create Mission Manager'.", null); return; }

            if (!mission.cameraDirector) Add(MessageType.Warning, "No MissionCameraDirector assigned: heats will start without the fly-over.", mission);
            if (!mission.defaultAggroProfile) Add(MessageType.Warning, "Mission has no Default Aggro Profile.", mission);
            if (mission.heats.Count == 0) Add(MessageType.Error, "Mission has no heats.", mission);
            if (mission.beginOnStart) Add(MessageType.Warning, "'Begin On Start' is ticked. Untick it before a build so GameExecutionManager decides when the mission runs.", mission);

            var gem = FindInScene<GameExecutionManager>().FirstOrDefault();
            if (!gem) Add(MessageType.Warning, "No GameExecutionManager in the open scene. Open YannicksWorld.", null);

            var bikes = FindInScene<MotorcycleController>().Where(b => b.gameObject.activeInHierarchy).ToList();
            GameObject start = FindObjectNamed("AI Chat Interaction") ?? FindObjectNamed("Sirihanna");
            Add(MessageType.Info, "Minimap: its camera shows about 15 m around the player, so a fan further away than that appears on the rim as a direction marker. That is expected.", null);

            var path = new NavMeshPath();
            for (int i = 0; i < mission.heats.Count; i++)
            {
                var fan = mission.heats[i];
                string tag = $"Heat {i + 1}";
                if (!fan) { Add(MessageType.Error, $"{tag}: empty slot in the Heats list.", mission); continue; }
                tag = $"Heat {i + 1} ({fan.fanDisplayName})";

                ValidateWaves(mission, fan, tag);

                bool fanOnNav = NavMesh.SamplePosition(fan.transform.position, out var fanHit, 1.5f, NavMesh.AllAreas);
                if (!fanOnNav) Add(MessageType.Error, $"{tag}: fan is not on the NavMesh, so enemies can't path to them.", fan);

                var points = fan.spawnPoints == null ? new List<Transform>() : fan.spawnPoints.Where(s => s != null).ToList();
                if (points.Count == 0) Add(MessageType.Error, $"{tag}: no spawn points. Use 'Auto-Place Spawn Points'.", fan);
                else
                {
                    int ok = 0;
                    foreach (var sp in points)
                    {
                        if (!NavMesh.SamplePosition(sp.position, out var spHit, 2f, NavMesh.AllAreas)) { Add(MessageType.Error, $"{tag}: {sp.name} is off the NavMesh.", sp); continue; }
                        if (!fanOnNav) continue;
                        if (!NavMesh.CalculatePath(spHit.position, fanHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                        { Add(MessageType.Error, $"{tag}: {sp.name} has no complete path to the fan.", sp); continue; }

                        float len = PathLength(path);
                        if (len < 10f) Add(MessageType.Warning, $"{tag}: {sp.name} is only {len:0} m from the fan, so enemies arrive almost instantly.", sp);
                        else if (len > maxPathLength) Add(MessageType.Warning, $"{tag}: {sp.name} path is {len:0} m. Enemies walk to the fan, so they take about {len / 2.5f:0} s to arrive.", sp);
                        if (Vector3.Distance(sp.position, fan.transform.position) < fan.arrivalRadius)
                            Add(MessageType.Warning, $"{tag}: {sp.name} is inside the arrival radius, so a portal may open on top of the player.", sp);
                        foreach (var other in mission.heats)
                            if (other && other != fan && Vector3.Distance(sp.position, other.transform.position) < other.arrivalRadius)
                                Add(MessageType.Warning, $"{tag}: {sp.name} is inside {other.fanDisplayName}'s arrival radius. The fans are too close together.", sp);
                        if (!Physics.Linecast(fan.FocusPoint, sp.position + Vector3.up * 1.2f, occlusionMask, QueryTriggerInteraction.Ignore))
                            Add(MessageType.Info, $"{tag}: {sp.name} is in plain view of the fan (the portal opens in the open).", sp);
                        ok++;
                    }
                    if (ok < 2) Add(MessageType.Warning, $"{tag}: only {ok} usable spawn point(s), so waves funnel in from one direction.", fan);
                }

                if (!fan.ring) Add(MessageType.Warning, $"{tag}: no golden ring assigned.", fan);
                if (!fan.minimapBlip) Add(MessageType.Warning, $"{tag}: no minimap blip.", fan);
                var stations = fan.restockStations == null ? new List<AmmoRestockStation>() : fan.restockStations.Where(s => s != null).ToList();
                if (stations.Count == 0) Add(MessageType.Warning, $"{tag}: no ammo restock station.", fan);
                foreach (var st in stations)
                {
                    var col = st.GetComponent<Collider>();
                    if (!col) Add(MessageType.Error, $"{tag}: restock '{st.name}' has no collider.", st);
                    else if (!col.isTrigger) Add(MessageType.Error, $"{tag}: restock '{st.name}' collider isn't a trigger.", st);
                    float d = Vector3.Distance(st.transform.position, fan.transform.position);
                    if (d > 20f) Add(MessageType.Warning, $"{tag}: restock '{st.name}' is {d:0} m from the fan (aim for 8-15 m).", st);
                }

                // ---- travel to this fan
                Transform from = i > 0 && mission.heats[i - 1] ? mission.heats[i - 1].transform : (start ? start.transform : null);
                if (!from) continue;
                string fromName = i > 0 ? mission.heats[i - 1].fanDisplayName : "Sirihanna";
                bool timed = i > 0 || mission.timeFirstFan;
                float timer = fan.travelSeconds > 0f ? fan.travelSeconds : mission.intermissionSeconds;

                float straight = Vector3.Distance(from.position, fan.transform.position);
                float route = straight;
                bool hasRoute = NavMesh.SamplePosition(from.position, out var fromHit, 3f, NavMesh.AllAreas) && fanOnNav &&
                                NavMesh.CalculatePath(fromHit.position, fanHit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                if (hasRoute) route = PathLength(path);
                else Add(MessageType.Warning, $"{tag}: no walkable NavMesh route from {fromName} (straight line {straight:0} m). Check it by hand.", fan);

                float travel = Mathf.Max(0f, route - fan.arrivalRadius);
                float walk = travel / Mathf.Max(0.1f, walkSpeed), sprint = travel / Mathf.Max(0.1f, sprintSpeed), ride = travel / Mathf.Max(0.1f, bikeSpeed);

                var bike = bikes.OrderBy(b => Vector3.Distance(b.transform.position, from.position)).FirstOrDefault();
                string bikeNote = bike ? $" Nearest bike to {fromName}: '{bike.name}', {Vector3.Distance(bike.transform.position, from.position):0} m away." : " No bikes found in the scene.";

                if (i > 0 && straight < minFanSeparation)
                    Add(MessageType.Warning, $"{tag}: only {straight:0} m from {fromName} ({walk:0} s walk). That isn't 'around the corner' and doesn't call for a vehicle. Move this fan further out.", fan);

                if (timed)
                {
                    var t = sprint > timer && ride > timer ? MessageType.Error : walk > timer ? MessageType.Warning : MessageType.Info;
                    string verdict = t == MessageType.Error ? " Not reachable in time even on a bike: raise the timer or move the fan."
                                   : t == MessageType.Warning ? (sprint <= timer ? " Needs sprinting or a bike." : " Needs a bike.")
                                   : timer > walk * 2f ? $" The timer is more than double the walk, so it adds no pressure. Try about {Mathf.Ceil(walk * 1.2f / 5f) * 5f:0} s." : "";
                    Add(t, $"{tag}: {travel:0} m from {fromName} (straight line {straight:0} m). Walk {walk:0} s, sprint {sprint:0} s, bike {ride:0} s; timer {timer:0} s.{verdict}{(t == MessageType.Info ? "" : bikeNote)}", fan);
                }
                else Add(MessageType.Info, $"{tag}: {travel:0} m from {fromName} (walk {walk:0} s). This leg is untimed.", fan);

                if (i > 0 && !Physics.Linecast(mission.heats[i - 1].FocusPoint, fan.FocusPoint, occlusionMask, QueryTriggerInteraction.Ignore))
                    Add(MessageType.Info, $"{tag}: directly visible from {fromName}. The goal is 'around the corner'.", fan);
            }

            if (gem && gem.PlayerCar && !gem.PlayerCar.activeSelf)
                Add(MessageType.Info, "Vehicles: the 'Car' is switched off until the police chase, so during this mission the bikes are the only rides unless you activate the car earlier.", gem.PlayerCar);

            if (report.All(r => r.type != MessageType.Error && r.type != MessageType.Warning))
                Add(MessageType.Info, "No errors or warnings. Now playtest it.", mission);

            foreach (var (type, msg, ctx) in report)
            {
                if (type == MessageType.Error) Debug.LogError("[SaveTheFans] " + msg, ctx);
                else if (type == MessageType.Warning) Debug.LogWarning("[SaveTheFans] " + msg, ctx);
            }
            Repaint();
        }

        void ValidateWaves(SaveTheFansMission mission, FanHeat fan, string tag)
        {
            var set = fan.waveSet;
            if (!set) { Add(MessageType.Error, $"{tag}: no wave set.", fan); return; }
            if (set.waves.Count == 0) { Add(MessageType.Error, $"{tag}: wave set '{set.name}' has no waves.", set); return; }

            int prevTotal = -1;
            var checkedPrefabs = new HashSet<GameObject>();
            for (int w = 0; w < set.waves.Count; w++)
            {
                var wave = set.waves[w];
                int total = wave.TotalCount;
                if (total == 0) Add(MessageType.Error, $"{tag}: wave {w + 1} spawns nothing (missing prefabs?).", set);
                if (prevTotal >= 0 && total < prevTotal) Add(MessageType.Info, $"{tag}: wave {w + 1} is smaller than wave {w} ({total} < {prevTotal}). Intended?", set);
                prevTotal = total;

                foreach (var e in wave.entries)
                {
                    if (e == null) continue;
                    if (!e.prefab) { Add(MessageType.Warning, $"{tag}: wave {w + 1} entry '{e.label}' has no prefab.", set); continue; }
                    if (!e.aggroProfile && !mission.defaultAggroProfile) Add(MessageType.Warning, $"{tag}: '{e.label}' has no aggro profile and the mission has no default.", set);
                    if (!checkedPrefabs.Add(e.prefab)) continue;

                    if (!e.prefab.GetComponent<JUHealth>())
                        Add(MessageType.Error, $"{tag}: prefab '{e.prefab.name}' has no JUHealth on its root, so kills can't be detected.", e.prefab);
                    if (!e.prefab.GetComponent<JUCharacterArtificialInteligenceBrain>())
                        Add(MessageType.Error, $"{tag}: prefab '{e.prefab.name}' has no ZombieAI / PatrolAI on its root, so it can't be steered to the fan.", e.prefab);
                }
            }
        }

        void Add(MessageType t, string msg, Object ctx) => report.Add((t, msg, ctx));
    }
}
