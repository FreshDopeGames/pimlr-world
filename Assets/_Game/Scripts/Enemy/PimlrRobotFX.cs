using JUTPS;
using UnityEngine;

/// <summary>
/// PIMLR: the enemies are robots. Attach() gives an enemy glowing red eyes (RobotEyeGlow) and an electric
/// short-circuit when it dies and ragdolls (RobotShockEffect).
/// Called from PimlrEnemySpawnDirector.Arrive, which every spawner goes through (JUAutoInstantiate Story zones,
/// InfiniteMode waves, GameExecutionManager boss) - so no prefab needs editing.
/// Look & feel: Assets/_Game/Resources/PimlrRobotFX.asset.
///
/// Editor only: press F9 in Play Mode to kill the robot nearest the camera and watch the effect.
/// </summary>
public static class PimlrRobotFX
{
    const string SettingsResource = "PimlrRobotFX";

    static PimlrRobotFXSettings settings;
    static bool settingsLoaded;
    static Material arcMaterial, glowMaterial;
    static Texture2D beamTexture, dotTexture;
    static Mesh quadMesh;
    static Camera cachedCamera;
    static int cameraFrame = -1;

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int CullId = Shader.PropertyToID("_Cull");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        settings = null; settingsLoaded = false;
        arcMaterial = null; glowMaterial = null;
        beamTexture = null; dotTexture = null; quadMesh = null;
        cachedCamera = null; cameraFrame = -1;
    }

    public static PimlrRobotFXSettings Settings
    {
        get
        {
            if (!settingsLoaded)
            {
                settingsLoaded = true;
                settings = Resources.Load<PimlrRobotFXSettings>(SettingsResource);
                if (!settings)
                {
                    settings = ScriptableObject.CreateInstance<PimlrRobotFXSettings>();
                    Debug.LogWarning("PIMLR Robot FX: Resources/PimlrRobotFX.asset missing - using defaults (no materials = no arcs or halos).");
                }
            }
            return settings;
        }
    }

    /// <summary>Make this enemy a robot. Safe to call more than once. Skips anything that isn't a humanoid.</summary>
    public static void Attach(GameObject enemy)
    {
        if (!enemy || !Settings.robotFXEnabled) return;
        Animator anim = enemy.GetComponent<Animator>();
        if (!anim) anim = enemy.GetComponentInChildren<Animator>(true);
        if (!anim || !anim.isHuman) return;

        if (!enemy.TryGetComponent(out RobotEyeGlow _)) enemy.AddComponent<RobotEyeGlow>();
        if (!enemy.TryGetComponent(out RobotShockEffect _)) enemy.AddComponent<RobotShockEffect>();
    }

    // ---------------------------------------------------------------- shared runtime resources

    public static Camera MainCamera
    {
        get
        {
            if (cameraFrame != Time.frameCount)
            {
                cameraFrame = Time.frameCount;
                if (!cachedCamera || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
            }
            return cachedCamera;
        }
    }

    /// <summary>Electric arc material (soft beam with a white-hot core). Shared by every robot.</summary>
    public static Material ArcMaterial
    {
        get
        {
            if (!arcMaterial && Settings.additiveMaterial)
                arcMaterial = MakeAdditive("PIMLR_RobotArc (runtime)", BeamTexture, Settings.arcColor);
            return arcMaterial;
        }
    }

    /// <summary>Soft round glow (sparks, eye halos). White - tinted per renderer / per particle.</summary>
    public static Material GlowMaterial
    {
        get
        {
            if (!glowMaterial && Settings.additiveMaterial)
                glowMaterial = MakeAdditive("PIMLR_RobotGlow (runtime)", DotTexture, Color.white);
            return glowMaterial;
        }
    }

    public static Mesh QuadMesh
    {
        get
        {
            if (!quadMesh)
            {
                quadMesh = new Mesh { name = "PIMLR_RobotFXQuad" };
                quadMesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                };
                quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                quadMesh.RecalculateBounds();
            }
            return quadMesh;
        }
    }

    static Material MakeAdditive(string name, Texture2D tex, Color color)
    {
        var m = new Material(Settings.additiveMaterial) { name = name };
        if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, tex);
        if (m.HasProperty(MainTexId)) m.SetTexture(MainTexId, tex);
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
        if (m.HasProperty(CullId)) m.SetFloat(CullId, 0f); // render both sides
        return m;
    }

    // Across the beam (v): bright narrow core + soft falloff. Along it (u): flat.
    static Texture2D BeamTexture
    {
        get
        {
            if (!beamTexture)
            {
                const int w = 4, h = 32;
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float d = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                    float a = Mathf.Clamp01(Mathf.Exp(-d * d / 0.05f) + 0.45f * Mathf.Pow(1f - d, 3f));
                    byte b = (byte)Mathf.RoundToInt(a * 255f);
                    for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, b);
                }
                beamTexture = MakeTexture("PIMLR_RobotBeam", w, h, px);
            }
            return beamTexture;
        }
    }

    static Texture2D DotTexture
    {
        get
        {
            if (!dotTexture)
            {
                const int n = 32;
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - r), 3f) + 0.8f * Mathf.Exp(-r * r * 40f));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                dotTexture = MakeTexture("PIMLR_RobotGlowDot", n, n, px);
            }
            return dotTexture;
        }
    }

    static Texture2D MakeTexture(string name, int w, int h, Color32[] px)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        t.SetPixels32(px);
        t.Apply(false, true);
        return t;
    }

#if UNITY_EDITOR
    // ---------------------------------------------------------------- editor test key
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateDebugKeys()
    {
        var go = new GameObject("PIMLR_RobotFXDebug") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        go.AddComponent<PimlrRobotFXDebugKeys>();
    }

    class PimlrRobotFXDebugKeys : MonoBehaviour
    {
        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.f9Key.wasPressedThisFrame) return;

            Camera cam = MainCamera;
            Vector3 from = cam ? cam.transform.position : Vector3.zero;
            JUHealth best = null;
            float bestDist = float.MaxValue;
            foreach (var fx in FindObjectsByType<RobotShockEffect>(FindObjectsSortMode.None))
            {
                var h = fx.GetComponent<JUHealth>();
                if (!h || h.IsDead) continue;
                float d = (fx.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = h; }
            }

            if (best)
            {
                Debug.Log("PIMLR Robot FX: F9 - killing " + best.name + " to test the shock.", best);
                best.DoDamage(best.Health + 1000f);
            }
            else Debug.Log("PIMLR Robot FX: F9 - no living robot enemy in the scene yet.");
        }
    }
#endif
}
