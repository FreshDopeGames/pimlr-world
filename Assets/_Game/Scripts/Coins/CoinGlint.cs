// PIMLR World — Coin glint
// Builds a tiny star-sparkle ParticleSystem at runtime and hands it to CoinPickup.sparkle,
// so the prefab needs no particle assets. Plays while the coin idles.
// Shine itself comes from the material (metallic 1 / smoothness 0.9) + a Reflection Probe
// in the scene + URP Bloom — see README "Make it shiny".
//
// Unreal equivalent: a small Niagara sprite emitter attached to the coin mesh.
using UnityEngine;

namespace PIMLR.Coins
{
    [RequireComponent(typeof(CoinPickup))]
    public class CoinGlint : MonoBehaviour
    {
        public Color glintColor = new Color(1f, 0.95f, 0.75f, 1f);
        public float rate = 1.6f;
        public float size = 0.22f;
        [Tooltip("Assigned by PIMLR > Coins setup. Keep it set: Shader.Find fallback can fail in WebGL if the shader is stripped.")]
        public Material glintMaterial;

        static Material s_mat;

        void Awake()
        {
            var pickup = GetComponent<CoinPickup>();
            if (pickup.sparkle) return;

            var go = new GameObject("Glint");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = 0.35f; main.startSpeed = 0f;
            main.startSize = size; main.startColor = glintColor;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.13f;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0, 0), new Keyframe(0.35f, 1), new Keyframe(1, 0)));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = glintMaterial ? glintMaterial : GetMaterial();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            pickup.sparkle = ps;
        }

        static Material GetMaterial()
        {
            if (s_mat) return s_mat;
            s_mat = CreateGlintMaterial();
            return s_mat;
        }

        /// Public so the Editor setup can bake it into an asset.
        public static Material CreateGlintMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader) { name = "M_CoinGlint" };
            // additive
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetTexture("_BaseMap", MakeStar());
            m.SetColor("_BaseColor", Color.white * 1.6f);
            return m;
        }

        static Texture2D MakeStar()
        {
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + .5f) / S * 2 - 1, v = (y + .5f) / S * 2 - 1;
                float core = Mathf.Exp(-(u * u + v * v) * 18f);
                float rays = Mathf.Exp(-Mathf.Abs(u) * 40f) * Mathf.Exp(-Mathf.Abs(v) * 3f)
                           + Mathf.Exp(-Mathf.Abs(v) * 40f) * Mathf.Exp(-Mathf.Abs(u) * 3f);
                float a = Mathf.Clamp01(core + rays * 0.8f);
                px[y * S + x] = new Color(1, 1, 1, a);
            }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }
}
