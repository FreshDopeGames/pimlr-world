using UnityEngine;

/// <summary>
/// PIMLR: look & feel for the robot enemies - glowing red eyes, and the electric short-circuit when they die.
/// The live copy is Assets/_Game/Resources/PimlrRobotFX.asset - tune everything there, no code changes needed.
/// Applied to every enemy automatically by PimlrRobotFX.Attach (called from PimlrEnemySpawnDirector.Arrive).
/// </summary>
[CreateAssetMenu(menuName = "PIMLR/Robot FX Settings", fileName = "PimlrRobotFX")]
public class PimlrRobotFXSettings : ScriptableObject
{
    [Tooltip("Master switch: red eyes + death shock on every enemy.")]
    public bool robotFXEnabled = true;

    [Header("Materials (keep them referenced here so WebGL builds keep their shaders)")]
    [Tooltip("URP Particles/Unlit, Transparent, Additive. Arcs, sparks and eye halos are made from it at runtime.")]
    public Material additiveMaterial;
    [Tooltip("URP Unlit, Opaque. Replaces the eye material on Renderer_EyeLeft / Renderer_EyeRight.")]
    public Material eyeMaterial;

    [Header("Eyes")]
    [Tooltip("Child renderers whose name contains this become glowing eyes.")]
    public string eyeRendererName = "Renderer_Eye";
    [ColorUsage(false, true)] public Color eyeColor = new Color(1f, 0.04f, 0.02f);
    [Tooltip("Eye mesh brightness. Above 1 = solid, 'lit from inside' red.")]
    public float eyeBrightness = 1.6f;
    [Tooltip("Soft glow sprite on each eye. Fakes bloom, so it works with post-processing off.")]
    public bool eyeHalo = true;
    public float haloSize = 0.13f;
    public float haloIntensity = 1.1f;
    [Tooltip("Halos switch off beyond this distance from the camera.")]
    public float haloMaxDistance = 70f;
    public float pulseSpeed = 0.6f;
    [Range(0f, 1f)] public float pulseAmount = 0.3f;
    [Tooltip("Eyes flicker on as the robot steps out of its portal.")]
    public float bootUpTime = 0.5f;
    public float malfunctionHz = 18f;
    [Range(0f, 1f)] public float malfunctionDropChance = 0.45f;
    public float eyePowerDownTime = 0.6f;

    [Header("Death shock - timing")]
    public float shockDuration = 2.5f;
    [Tooltip("Shock strength over the shock's lifetime (0..1).")]
    public AnimationCurve shockStrength = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.15f, 0.9f), new Keyframe(0.6f, 0.55f), new Keyframe(1f, 0f));
    [Tooltip("Seconds between zaps at full strength (random range). Slows down as the shock fades.")]
    public Vector2 zapInterval = new Vector2(0.04f, 0.12f);
    [Tooltip("Robots dying at the same moment beyond this just power their eyes down (big Infinite waves).")]
    public int maxConcurrentShocks = 8;

    [Header("Death shock - arcs")]
    [ColorUsage(false, true)] public Color arcColor = new Color(1.3f, 1.7f, 2.8f);
    [Range(1, 12)] public int maxArcs = 5;
    [Range(3, 16)] public int arcSegments = 8;
    public float arcJitter = 0.06f;
    public Vector2 arcWidth = new Vector2(0.025f, 0.06f);
    public Vector2 arcLifetime = new Vector2(0.05f, 0.12f);
    [Tooltip("Max distance between two bones for an arc to jump between them.")]
    public float maxArcLength = 0.8f;
    [Range(0f, 1f)] public float strayArcChance = 0.3f;

    [Header("Death shock - sparks")]
    public bool sparks = true;
    public Vector2Int sparksPerZap = new Vector2Int(3, 8);
    public Vector2 sparkSpeed = new Vector2(1.5f, 4.5f);

    [Header("Death shock - ragdoll twitch")]
    public bool twitch = true;
    [Tooltip("m/s kick given to a random ragdoll bone each zap.")]
    public float twitchVelocity = 0.6f;
    [Tooltip("rad/s spin given to that bone.")]
    public float twitchSpin = 4f;

    [Header("Death shock - light")]
    public bool flickerLight = true;
    public Color lightColor = new Color(0.55f, 0.8f, 1f);
    public float lightIntensity = 3f;
    public float lightRange = 3f;
    [Tooltip("Dying robots that may own a flicker light at once (WebGL / URP per-object light limit).")]
    public int maxConcurrentLights = 2;

    [Header("Death shock - sound")]
    public AudioClip[] zapClips;
    [Range(0f, 1f)] public float zapVolume = 0.6f;
    [Range(0f, 1f)] public float zapSoundChance = 0.45f;
    public Vector2 zapPitch = new Vector2(0.85f, 1.25f);
    public float zapSoundMaxDistance = 35f;
}
