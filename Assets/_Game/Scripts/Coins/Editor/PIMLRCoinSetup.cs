// PIMLR World — Coin setup automation (Editor only). Unity 6.6 / URP.
// Menu: PIMLR > Coins > ...
//   1. Configure Textures         – sets normal-map / linear import settings
//   2. Build Material + Prefab    – URP Lit gold chrome material + PIMLR_Coin prefab
//   3. Add Spawner To Open Scene  – CoinSpawner in the active scene (YannicksWorld)
//   4. Add Collector To Player    – CoinCollector on the object with JUCharacterController
//   5. Add Drops To PIMLR_ Enemies – EnemyCoinDrop on every prefab whose name starts "PIMLR_"
//   Run All                        – 1→5 in order
// Every step is idempotent: re-running won't duplicate components.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using PIMLR.Coins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PIMLRCoinSetup
{
    const string Root    = "Assets/_Game/Art/Coin";
    const string TexDir  = Root + "/Textures";
    const string MeshDir = Root + "/Meshes";
    const string MatPath = Root + "/M_PIMLRCoin_Gold.mat";
    const string PrefabPath = "Assets/_Game/Prefabs/Pickups/PIMLR_Coin.prefab";

    [MenuItem("PIMLR/Coins/Run All", priority = 0)]
    public static void RunAll()
    {
        ConfigureTextures();
        BuildMaterialAndPrefab();
        AddSpawnerToScene();
        AddCollectorToPlayer();
        AddDropsToEnemies();
        Debug.Log("[PIMLR Coins] Setup complete. Enter Play mode and kill an enemy.");
    }

    [MenuItem("PIMLR/Coins/1. Configure Textures", priority = 11)]
    public static void ConfigureTextures()
    {
        SetTex(TexDir + "/T_PIMLRCoin_Normal_GL.png", ti => { ti.textureType = TextureImporterType.NormalMap; });
        SetTex(TexDir + "/T_PIMLRCoin_AO.png",        ti => { ti.sRGBTexture = false; });
        SetTex(TexDir + "/T_PIMLRCoin_Height.png",    ti => { ti.sRGBTexture = false; });
    }

    static void SetTex(string path, System.Action<TextureImporter> edit)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!ti) { Debug.LogWarning("[PIMLR Coins] Missing texture: " + path); return; }
        ti.maxTextureSize = 1024;
        ti.mipmapEnabled = true;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        edit(ti);
        ti.SaveAndReimport();
    }

    [MenuItem("PIMLR/Coins/2. Build Material + Prefab", priority = 12)]
    public static void BuildMaterialAndPrefab()
    {
        // ---- material: URP Lit, full metal, mirror-ish smoothness ----
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (!mat)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.SetColor("_BaseColor", new Color(1.0f, 0.74f, 0.32f));   // warm gold. For silver chrome: (0.92,0.93,0.95)
        mat.SetFloat("_Metallic", 1f);
        mat.SetFloat("_Smoothness", 0.9f);
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_PIMLRCoin_Normal_GL.png");
        if (nrm) { mat.SetTexture("_BumpMap", nrm); mat.SetFloat("_BumpScale", 1.2f); mat.EnableKeyword("_NORMALMAP"); }
        var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_PIMLRCoin_AO.png");
        if (ao) { mat.SetTexture("_OcclusionMap", ao); mat.SetFloat("_OcclusionStrength", 1f); mat.EnableKeyword("_OCCLUSIONMAP"); }
        // faint self-glow so coins pop in dark streets and catch Bloom
        mat.SetColor("_EmissionColor", new Color(0.35f, 0.22f, 0.05f) * 0.6f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.enableInstancing = true;  // GPU instancing: dozens of coins, few draw calls
        EditorUtility.SetDirty(mat);

        // ---- prefab ----
        var mesh = LoadMesh(MeshDir + "/SM_PIMLRCoin_Lo.fbx") ?? LoadMesh(MeshDir + "/SM_PIMLRCoin_Hi.fbx");
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var go = new GameObject("PIMLR_Coin");
        try
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var mf = visual.AddComponent<MeshFilter>();
            var mr = visual.AddComponent<MeshRenderer>();
            if (mesh) mf.sharedMesh = mesh;
            else
            {   // fallback until the FBX is exported
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mf.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tmp);
                visual.transform.localScale = new Vector3(0.3f, 0.0175f, 0.3f);
                Debug.LogWarning("[PIMLR Coins] Coin FBX not found in " + MeshDir + " — using a cylinder placeholder.");
            }
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // cheap; coins are tiny
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

            // Coin faces along +Y after FBX import: thin box = lands flat like a real coin
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.28f, 0.035f, 0.28f);
            var pm = new PhysicsMaterial("PM_Coin") { bounciness = 0.35f, dynamicFriction = 0.4f, staticFriction = 0.5f };
            col.sharedMaterial = pm;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.05f; rb.linearDamping = 0.2f; rb.angularDamping = 0.5f;

            go.AddComponent<CoinPickup>();
            var glint = go.AddComponent<CoinGlint>();
            glint.glintMaterial = GetOrCreateGlintMaterial();
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Debug.Log("[PIMLR Coins] Prefab saved: " + PrefabPath);
        }
        finally { Object.DestroyImmediate(go); }
        AssetDatabase.SaveAssets();
    }

    static Material GetOrCreateGlintMaterial()
    {
        const string path = Root + "/M_CoinGlint.mat";
        const string texPath = TexDir + "/T_CoinGlint.png";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing) return existing;
        var mat = CoinGlint.CreateGlintMaterial();
        // persist the procedural star texture so the material survives reloads
        var tex = mat.GetTexture("_BaseMap") as Texture2D;
        if (tex)
        {
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Mesh LoadMesh(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();

    [MenuItem("PIMLR/Coins/3. Add Spawner To Open Scene", priority = 13)]
    public static void AddSpawnerToScene()
    {
        var existing = Object.FindFirstObjectByType<CoinSpawner>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!existing)
        {
            var go = new GameObject("CoinSpawner");
            existing = go.AddComponent<CoinSpawner>();
            Undo.RegisterCreatedObjectUndo(go, "Add CoinSpawner");
        }
        if (prefab) existing.coinPrefab = prefab.GetComponent<CoinPickup>();
        EditorUtility.SetDirty(existing);
        EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
        Debug.Log("[PIMLR Coins] CoinSpawner ready in " + existing.gameObject.scene.name);
    }

    [MenuItem("PIMLR/Coins/4. Add Collector To Player", priority = 14)]
    public static void AddCollectorToPlayer()
    {
        // Find the JUTPS player controller by type name (no compile-time JUTPS dependency)
        var player = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(m => m.GetType().Name.StartsWith("JUCharacterController") &&
                                 (m.CompareTag("Player") || m.GetComponentInParent<Camera>() == null));
        GameObject target = player ? player.gameObject : GameObject.FindWithTag("Player");
        if (!target) { Debug.LogWarning("[PIMLR Coins] No player found in the open scene. Select it and add CoinCollector manually."); return; }
        if (!target.GetComponent<CoinCollector>()) Undo.AddComponent<CoinCollector>(target);
        EditorSceneManager.MarkSceneDirty(target.scene);
        Debug.Log("[PIMLR Coins] CoinCollector on " + target.name, target);
    }

    [MenuItem("PIMLR/Coins/5. Add Drops To PIMLR_ Enemies", priority = 15)]
    public static void AddDropsToEnemies()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("PIMLR_ t:Prefab"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(path).StartsWith("PIMLR_") || path == PrefabPath) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool isEnemy = root.GetComponentsInChildren<Component>(true).Any(c => c && c.GetType().Name == "JUHealth")
                               && !root.GetComponentsInChildren<Component>(true).Any(c => c && c.GetType().Name.StartsWith("JUCharacterController") && root.CompareTag("Player"));
                if (!isEnemy || root.GetComponent<EnemyCoinDrop>()) continue;
                var drop = root.AddComponent<EnemyCoinDrop>();
                // tougher enemies pay more
                string nm = root.name.ToLowerInvariant();
                if (nm.Contains("bomber")) { drop.minValue = 6; drop.maxValue = 10; }
                else if (nm.Contains("gunner")) { drop.minValue = 4; drop.maxValue = 7; }
                else { drop.minValue = 3; drop.maxValue = 5; }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                n++;
                Debug.Log("[PIMLR Coins] EnemyCoinDrop -> " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log($"[PIMLR Coins] Added EnemyCoinDrop to {n} PIMLR_ enemy prefab(s).");
    }
}
#endif
