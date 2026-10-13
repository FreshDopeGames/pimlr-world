using UnityEngine;

/// <summary>
/// PIMLR: look & feel for enemies arriving (portal / electric burst + sounds) and where they arrive from.
/// The live copy is Assets/_Game/Resources/PimlrSpawnFX.asset - swap effects there, no code changes needed.
/// </summary>
[CreateAssetMenu(menuName = "PIMLR/Enemy Spawn FX Settings", fileName = "PimlrSpawnFX")]
public class PimlrSpawnFXSettings : ScriptableObject
{
    [Header("Effects (any particle prefab)")]
    [Tooltip("Opens first, enemy steps out of it. e.g. Epic Toon FX SpinPortal / VortexPortal / WavePortal.")]
    public GameObject portalPrefab;
    [Tooltip("Plays the moment the enemy appears. e.g. LightningExplosion / NovaLightning / LightningStrikeTall.")]
    public GameObject arrivalBurstPrefab;
    public float portalScale = 1f;
    public float burstScale = 1f;
    [Tooltip("Raise the portal so its centre is around chest height.")]
    public float portalHeight = 1.1f;
    public float portalLifetime = 2.6f;
    public float burstLifetime = 2.5f;

    [Header("Sounds")]
    public AudioClip portalOpenSfx;
    public AudioClip arrivalSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.85f;
    public float sfxMaxDistance = 60f;

    [Header("Timing")]
    [Tooltip("Seconds between the portal opening and the enemy stepping out.")]
    public float stepThroughDelay = 0.65f;

    [Header("Where enemies come from")]
    [Tooltip("Spawn this far from the player at least (down the street, not in your face).")]
    public float minDistanceFromPlayer = 28f;
    public float maxDistanceFromPlayer = 55f;
    [Tooltip("Prefer points the camera can't see (around corners / behind you).")]
    public bool preferOffScreen = true;
    [Tooltip("Ignore points more than this far above/below the player (rooftops, underpasses).")]
    public float maxHeightDifference = 3f;
    [Tooltip("Don't reuse a spawn spot within this radius for a couple of seconds, so arrivals spread out.")]
    public float spreadRadius = 8f;
}
