// PIMLR #STF (Save The Fans) — the golden ring around a fan.
// A copy of Sirihanna's ring ("GlowCircle" under "AI Chat Interaction": a looping mesh-particle
// system whose cyan comes from the particle Start Color) recolored gold at runtime. The original
// ring and its material are not touched.
//
//   * Particle systems: Start Color is replaced, keeping each system's original alpha (the
//     GlowCircle uses a soft 0.18 alpha; forcing 1.0 would blow it out). New particles pick the
//     color up within about a second, which is how the ring drifts gold -> red as the fan gets washed.
//   * Mesh / sprite renderers (if you add any, e.g. a beacon): tinted with a MaterialPropertyBlock.
//   * Pulse: the ring object's scale breathes, faster as danger rises. Scale works for any ring type.
//
// Unreal note: a Niagara ring with a "Danger" user parameter (0..1) lerping gold -> red, and the
// pulse driven in the emitter. The beacon is a Niagara beam or a tall additive cylinder.

using UnityEngine;

namespace PIMLR.Missions.SaveTheFans
{
    [AddComponentMenu("PIMLR/Save The Fans/Fan Glow Ring")]
    public class FanGlowRing : MonoBehaviour
    {
        [Header("Colors")]
        public Color gold = new Color(1f, 0.76f, 0.18f, 1f);
        public Color danger = new Color(1f, 0.12f, 0.08f, 1f);
        public Color celebrate = new Color(0.8f, 0.98f, 0.32f, 1f); // brand neon green #CDF952
        [Tooltip("Multiplies the original particle alpha. Raise it if gold reads weaker than Sirihanna's cyan.")]
        [Range(0.5f, 4f)] public float alphaBoost = 1.5f;

        [Header("Pulse (scale)")]
        [Min(0f)] public float pulseSpeed = 0.8f;
        [Range(0f, 0.3f)] public float pulseAmount = 0.06f;
        [Tooltip("Pulse gets this much faster at full danger.")]
        [Min(1f)] public float dangerPulseMultiplier = 3f;

        [Header("Mesh / sprite renderers only")]
        [Tooltip("Shader color properties to tint on non-particle renderers.")]
        public string[] colorProperties = { "_BaseColor", "_Color", "_TintColor" };

        [Header("Travel beacon (optional)")]
        [Tooltip("Tall light beam child shown only while the player is travelling to this fan.")]
        public GameObject beacon;

        ParticleSystem[] systems = new ParticleSystem[0];
        float[] baseAlpha = new float[0];
        Renderer[] meshRenderers = new Renderer[0];
        MaterialPropertyBlock mpb;
        int[] propIds;
        Vector3 baseScale;
        float danger01;
        float celebrateUntil;
        bool initialized;
        bool visible = true;

        void Awake() => Init();

        void Init()
        {
            if (initialized) return;
            initialized = true;
            baseScale = transform.localScale;

            systems = GetComponentsInChildren<ParticleSystem>(true);
            baseAlpha = new float[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var start = systems[i].main.startColor;
                // Constant / gradient / two-color modes all expose a representative max color.
                baseAlpha[i] = start.mode == ParticleSystemGradientMode.Color ? start.color.a : start.colorMax.a;
                if (baseAlpha[i] <= 0f) baseAlpha[i] = 1f;
            }

            var all = GetComponentsInChildren<Renderer>(true);
            meshRenderers = System.Array.FindAll(all, r => !(r is ParticleSystemRenderer));
            mpb = new MaterialPropertyBlock();
            propIds = new int[colorProperties.Length];
            for (int i = 0; i < colorProperties.Length; i++) propIds[i] = Shader.PropertyToID(colorProperties[i]);

            Apply(gold);
        }

        /// Shows/hides the ring's particles and renderers (not the GameObject, so this script keeps running).
        public void SetVisible(bool on)
        {
            Init();
            visible = on;
            foreach (var r in meshRenderers) if (r && !IsBeacon(r.transform)) r.enabled = on;
            foreach (var ps in systems)
            {
                if (!ps || IsBeacon(ps.transform)) continue;
                if (on) ps.Play(true);
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (!on) SetBeacon(false);
        }

        public void SetBeacon(bool on) { if (beacon && beacon.activeSelf != on) beacon.SetActive(on); }
        public void SetDanger(float value01) => danger01 = Mathf.Clamp01(value01);
        public void Celebrate(float seconds = 3f) => celebrateUntil = Time.time + seconds;

        bool IsBeacon(Transform t) => beacon && (t == beacon.transform || t.IsChildOf(beacon.transform));

        void Update()
        {
            if (!visible) return;

            Apply(Time.time < celebrateUntil ? celebrate : Color.Lerp(gold, danger, danger01));

            float speed = pulseSpeed * Mathf.Lerp(1f, dangerPulseMultiplier, danger01);
            float s = 1f + pulseAmount * Mathf.Sin(Time.time * speed * Mathf.PI * 2f);
            transform.localScale = baseScale * s;
        }

        void Apply(Color c)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                if (!systems[i]) continue;
                var main = systems[i].main;
                var pc = c;
                pc.a = Mathf.Clamp01(baseAlpha[i] * alphaBoost);
                main.startColor = new ParticleSystem.MinMaxGradient(pc);
            }

            foreach (var r in meshRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                for (int i = 0; i < propIds.Length; i++) mpb.SetColor(propIds[i], c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
