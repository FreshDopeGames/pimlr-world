using System.Collections.Generic;
using UnityEngine;

// PIMLR (playtest): tints every renderer under this object blue while the Freeze Ray has it frozen,
// with a slight white glow (soft emission where the shader supports it + a small shimmering point light).
// JUHealth adds this component the first time an enemy is frozen, so no prefab needs editing.
// Uses per-material MaterialPropertyBlocks, so the shared materials are never changed.
[DisallowMultipleComponent]
public class FrostTint : MonoBehaviour
{
    [Tooltip("The colour the model is pushed toward while frozen.")]
    [SerializeField] private Color frostColor = new Color(0.35f, 0.65f, 1f, 1f);
    [Tooltip("0 = no tint, 1 = fully frostColor.")]
    [Range(0f, 1f)] [SerializeField] private float strength = 1f;

    [Header("Glow (PIMLR)")]
    [Tooltip("Soft white emission added to materials that support it (URP Lit with Emission on, glTF / RPM avatars).")]
    [SerializeField] private Color glowColor = new Color(0.85f, 0.93f, 1f, 1f);
    [Range(0f, 1f)] [SerializeField] private float glowEmission = 0.3f;
    [Tooltip("A small point light around the frozen enemy, so the glow shows on every shader and lights the ground.")]
    [SerializeField] private float glowLightIntensity = 1.6f;
    [SerializeField] private float glowLightRange = 2.6f;
    [Tooltip("Gentle shimmer: light intensity varies by this share.")]
    [Range(0f, 0.5f)] [SerializeField] private float glowPulse = 0.18f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit, Simple Lit, Unlit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in and legacy shaders
    private static readonly int GltfColorId = Shader.PropertyToID("baseColorFactor"); // glTFast / Ready Player Me avatars (Shader Graphs/glTF-pbrMetallicRoughness)
    private static readonly HashSet<string> warnedShaders = new HashSet<string>();
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");     // URP Lit / Simple Lit
    private static readonly int GltfEmissionId = Shader.PropertyToID("emissiveFactor"); // glTFast / RPM avatars

    private struct Slot
    {
        public Renderer renderer;
        public int materialIndex;
        public int colorId;
        public Color original;
        public int emissionId;      // -1 = this material can't glow
        public Color originalEmission;
    }

    private Light glowLight;

    private readonly List<Slot> slots = new List<Slot>();
    private MaterialPropertyBlock block;
    private bool isFrozen;

    public static void Freeze(GameObject target)
    {
        if (target == null) return;
        FrostTint tint = target.GetComponent<FrostTint>();
        if (tint == null) tint = target.AddComponent<FrostTint>();
        tint.ApplyTint();
    }

    public static void Thaw(GameObject target)
    {
        if (target == null) return;
        FrostTint tint = target.GetComponent<FrostTint>();
        if (tint != null) tint.ClearTint();
    }

    private void OnDisable()
    {
        // A deactivated enemy never runs its pending thaw, so clear the tint here.
        ClearTint();
    }

    private void ApplyTint()
    {
        if (isFrozen) return;
        if (block == null) block = new MaterialPropertyBlock();

        slots.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            // Only the model turns blue, not particle effects or trails.
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                int id = m.HasProperty(BaseColorId) ? BaseColorId : (m.HasProperty(GltfColorId) ? GltfColorId : (m.HasProperty(ColorId) ? ColorId : -1));
                if (id < 0)
                {
                    if (warnedShaders.Add(m.shader.name))
                        Debug.LogWarning("FrostTint: shader '" + m.shader.name + "' has no _BaseColor, baseColorFactor or _Color, so it will not turn blue.");
                    continue;
                }

                // Emission only shows where the shader computes it: URP Lit needs its Emission keyword on;
                // the glTF Shader Graph always applies emissiveFactor.
                int eid = (m.HasProperty(EmissionId) && m.IsKeywordEnabled("_EMISSION")) ? EmissionId
                        : (m.HasProperty(GltfEmissionId) ? GltfEmissionId : -1);
                slots.Add(new Slot { renderer = r, materialIndex = i, colorId = id, original = m.GetColor(id),
                                     emissionId = eid, originalEmission = eid >= 0 ? m.GetColor(eid) : Color.black });
            }
        }

        foreach (Slot s in slots)
        {
            Color tinted = Color.Lerp(s.original, frostColor, strength);
            tinted.a = s.original.a;

            s.renderer.GetPropertyBlock(block, s.materialIndex);
            block.SetColor(s.colorId, tinted);
            if (s.emissionId >= 0)
                block.SetColor(s.emissionId, s.originalEmission + glowColor * glowEmission);
            s.renderer.SetPropertyBlock(block, s.materialIndex);
        }

        ShowGlowLight(true);
        isFrozen = true;
    }

    private void ShowGlowLight(bool on)
    {
        if (!on)
        {
            if (glowLight != null) glowLight.enabled = false;
            return;
        }
        if (glowLightIntensity <= 0f) return;
        if (glowLight == null)
        {
            var go = new GameObject("FrostGlow");
            go.transform.SetParent(transform, false);
            glowLight = go.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.shadows = LightShadows.None;
            glowLight.renderMode = LightRenderMode.ForcePixel;
        }
        // Centre it on the body (renderer bounds), so it wraps the whole model.
        Bounds b = new Bounds(transform.position + Vector3.up, Vector3.zero);
        bool any = false;
        foreach (Slot s in slots)
            if (s.renderer != null) { if (!any) { b = s.renderer.bounds; any = true; } else b.Encapsulate(s.renderer.bounds); }
        glowLight.transform.position = b.center;
        glowLight.color = glowColor;
        glowLight.range = glowLightRange;
        glowLight.intensity = glowLightIntensity;
        glowLight.enabled = true;
    }

    private void Update()
    {
        if (!isFrozen || glowLight == null || !glowLight.enabled || glowPulse <= 0f) return;
        float phase = Time.time * 4f + GetHashCode() * 0.37f;   // each enemy shimmers on its own beat
        glowLight.intensity = glowLightIntensity * (1f - glowPulse + glowPulse * (0.5f + 0.5f * Mathf.Sin(phase)));
    }

    private void ClearTint()
    {
        if (!isFrozen) return;

        foreach (Slot s in slots)
            if (s.renderer != null) s.renderer.SetPropertyBlock(null, s.materialIndex);

        slots.Clear();
        ShowGlowLight(false);
        isFrozen = false;
    }
}