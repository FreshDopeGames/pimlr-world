using UnityEngine;
using UnityEngine.Audio;

// PIMLR (playtest): plays the base soundtrack and ducks it while a catalog song is playing.
// Watches the catalog's AudioSource (MusicSystem.musicSystem), so MusicSystem itself needs no changes.
public class SoundtrackDucker : MonoBehaviour
{
    [Header("Soundtrack")]
    [Tooltip("The AudioSource playing the base soundtrack. Found on this object if left empty.")]
    [SerializeField] private AudioSource soundtrack;
    [SerializeField] private bool playOnStart = true;

    [Header("Levels (dB)")]
    [Tooltip("Level while a catalog song is playing. -40 dB is nearly silent.")]
    [Range(-80f, 0f)] [SerializeField] private float duckedDb = -40f;
    [Tooltip("Level when no catalog song is playing. 0 dB = the AudioSource's own Volume.")]
    [Range(-80f, 0f)] [SerializeField] private float normalDb = 0f;

    [Header("Timing (seconds)")]
    [SerializeField] private float duckFadeSeconds = 0.75f;
    [SerializeField] private float restoreFadeSeconds = 2.5f;
    [Tooltip("The catalog must be silent this long before the soundtrack starts coming back. Stops pumping when a song is paused briefly.")]
    [SerializeField] private float restoreDelaySeconds = 0.75f;

    [Header("Optional AudioMixer")]
    [Tooltip("Leave empty to fade the AudioSource volume. Assign a mixer to fade an exposed parameter instead.")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string mixerParameter = "SoundtrackVolume";

    private MusicSystem catalog;
    private float baseVolume;
    private float currentDb;
    private float appliedDb = float.NaN;
    private float silentTime;

    private void Awake()
    {
        if (soundtrack == null) soundtrack = GetComponent<AudioSource>();
        if (soundtrack == null)
        {
            Debug.LogError("SoundtrackDucker: no AudioSource found on " + name + ".", this);
            enabled = false;
            return;
        }

        if (mixer != null && soundtrack.outputAudioMixerGroup == null)
            Debug.LogWarning("SoundtrackDucker: a mixer is assigned but the soundtrack AudioSource has no Output group, so ducking will not be heard.", this);

        baseVolume = soundtrack.volume;
        soundtrack.loop = true;
        soundtrack.spatialBlend = 0f;
        currentDb = normalDb;
        Apply();
    }

    private void Start()
    {
        if (playOnStart && !soundtrack.isPlaying) soundtrack.Play();
    }

    private void OnDestroy()
    {
        // A mixer parameter keeps its value after this scene unloads, so put it back.
        if (mixer != null) mixer.SetFloat(mixerParameter, normalDb);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float range = Mathf.Max(0.01f, normalDb - duckedDb);

        if (IsCatalogPlaying())
        {
            silentTime = 0f;
            currentDb = Mathf.MoveTowards(currentDb, duckedDb, range / Mathf.Max(0.01f, duckFadeSeconds) * dt);
        }
        else
        {
            silentTime += dt;
            if (silentTime >= restoreDelaySeconds)
                currentDb = Mathf.MoveTowards(currentDb, normalDb, range / Mathf.Max(0.01f, restoreFadeSeconds) * dt);
        }

        Apply();
    }

    private bool IsCatalogPlaying()
    {
        if (catalog == null && SceneManagerScript.Instance != null)
            catalog = SceneManagerScript.Instance.musicSystem;
        return catalog != null && catalog.musicSystem != null && catalog.musicSystem.isPlaying;
    }

    private void Apply()
    {
        if (Mathf.Approximately(currentDb, appliedDb)) return;
        appliedDb = currentDb;

        if (mixer != null)
            mixer.SetFloat(mixerParameter, currentDb);
        else
            soundtrack.volume = currentDb <= -80f ? 0f : baseVolume * Mathf.Pow(10f, currentDb / 20f);
    }
}