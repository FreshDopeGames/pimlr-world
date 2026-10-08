using System.Collections.Generic;
using UnityEngine;

// PIMLR (playtest): tints every renderer under this object blue while the Freeze Ray has it frozen.
// JUHealth adds this component the first time an enemy is frozen, so no prefab needs editing.
// Uses per-material MaterialPropertyBlocks, so the shared materials are never changed.
[DisallowMultipleComponent]
public class FrostTint : MonoBehaviour
{
    [Tooltip("The colour the model is pushed toward while frozen.")]
    [SerializeField] private Color frostColor = new Color(0.35f, 0.65f, 1f, 1f);
    [Tooltip("0 = no tint, 1 = fully frostColor.")]
    [Range(0f, 1f)] [SerializeField] private float strength = 0.7f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit, Simple Lit, Unlit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in and legacy shaders
    private static readonly int GltfColorId = Shader.PropertyToID("baseColorFactor"); // glTFast / Ready Player Me avatars (Shader Graphs/glTF-pbrMetallicRoughness)
    private static readonly HashSet<string> warnedShaders = new HashSet<string>();

    private struct Slot
    {
        public Renderer renderer;
        public int materialIndex;
        public int colorId;
        public Color original;
    }

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

                slots.Add(new Slot { renderer = r, materialIndex = i, colorId = id, original = m.GetColor(id) });
            }
        }

        foreach (Slot s in slots)
        {
            Color tinted = Color.Lerp(s.original, frostColor, strength);
            tinted.a = s.original.a;

            s.renderer.GetPropertyBlock(block, s.materialIndex);
            block.SetColor(s.colorId, tinted);
            s.renderer.SetPropertyBlock(block, s.materialIndex);
        }

        isFrozen = true;
    }

    private void ClearTint()
    {
        if (!isFrozen) return;

        foreach (Slot s in slots)
            if (s.renderer != null) s.renderer.SetPropertyBlock(null, s.materialIndex);

        slots.Clear();
        isFrozen = false;
    }
}