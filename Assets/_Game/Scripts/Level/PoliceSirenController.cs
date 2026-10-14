using System;
using JUTPS;
using UnityEngine;

/// <summary>
/// PIMLR: the police cruiser's siren in YannicksWorld (Zone Boss 2, "Shut Down Police Cruiser").
///  - Fades 0 -> 1 the first time RCC_AICarController locks onto the player (targetChase != null).
///  - Below <see cref="lowHealthThreshold"/> HP the pitch sinks smoothly toward <see cref="minPitch"/> (losing power).
///  - On defeat the siren winds down and fades out while the car stays put and the green "HATER FREE!" cinematic plays.
///    Only after that does JUHealth.OnVideoPlay hide the cars and start the ending FMV.
///
/// RCC_CarControllerV3 only manages the AudioSources it creates itself at runtime, so this script owns the siren
/// AudioSource you added under "All Audio Sources". It is routed through RCC's mixer group so it sits in the same mix.
///
/// Setup: add to the "Police (AI Chaser)" root (same object as JUHealth / RCC_AICarController). Everything auto-wires.
/// </summary>
[DisallowMultipleComponent]
public class PoliceSirenController : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    [Tooltip("The siren AudioSource under 'All Audio Sources'. Auto-found by a clip name containing 'siren'.")]
    [SerializeField] AudioSource siren;
    [SerializeField] JUHealth health;
    [SerializeField] RCC_AICarController ai;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] float maxVolume = 1f;
    [SerializeField] float fadeInSeconds = 2f;
    [SerializeField] float fadeOutSeconds = 2.5f;

    [Header("Losing power")]
    [SerializeField] float lowHealthThreshold = 75f;   // of 500
    [SerializeField] float normalPitch = 1f;
    [Range(0.1f, 1f)] [SerializeField] float minPitch = 0.45f;      // pitch at 0 HP
    [Range(0.05f, 1f)] [SerializeField] float defeatPitch = 0.2f;   // wind-down while fading out
    [Tooltip("Seconds for pitch to catch up with its target. Higher = smoother, lazier.")]
    [SerializeField] float pitchSmoothSeconds = 0.5f;
    [SerializeField] float defeatPitchSmoothSeconds = 1.2f;

    [Header("Defeat cinematic")]
    [SerializeField] string defeatWord = "HATER FREE!";
    [SerializeField] Color defeatColor = new Color32(57, 255, 20, 255); // neon green

    enum State { Idle, Pursuing, Defeated }
    State state = State.Idle;
    float pitchVelocity;
    bool pausedByGame;

    /// <summary>True once the defeat sequence has been started (JUHealth uses this to avoid re-entering).</summary>
    public bool DefeatHandled { get; private set; }

    void Awake()
    {
        if (health == null) health = GetComponent<JUHealth>();
        if (ai == null) ai = GetComponent<RCC_AICarController>();
        if (siren == null) siren = FindSiren();
        if (siren == null)
        {
            Debug.LogError("PoliceSirenController: no siren AudioSource found under 'All Audio Sources'.", this);
            enabled = false;
            return;
        }

        siren.playOnAwake = false;
        siren.loop = true;
        siren.volume = 0f;
        siren.pitch = normalPitch;
        if (siren.outputAudioMixerGroup == null && RCC_Settings.Instance != null)
            siren.outputAudioMixerGroup = RCC_Settings.Instance.audioMixer;
    }

    AudioSource FindSiren()
    {
        Transform container = transform.Find("All Audio Sources");
        AudioSource[] sources = container != null
            ? container.GetComponentsInChildren<AudioSource>(true)
            : GetComponentsInChildren<AudioSource>(true);
        foreach (AudioSource s in sources)
            if (s.clip != null && s.clip.name.ToLowerInvariant().Contains("siren")) return s;
        return null;
    }

    void OnDisable()
    {
        // AiCar.SetActive(false) (end of level / reload) - make sure nothing lingers.
        if (siren != null) siren.Stop();
    }

    void OnEnable()
    {
        // Re-armed each time GameExecutionManager activates the cruiser.
        if (siren == null) return;
        state = State.Idle;
        DefeatHandled = false;
        siren.volume = 0f;
        siren.pitch = normalPitch;
        pitchVelocity = 0f;
    }

    /// <summary>Starts the siren. Called automatically when the AI first locks onto the player; public for other triggers.</summary>
    public void BeginPursuit()
    {
        if (state != State.Idle || siren == null) return;
        state = State.Pursuing;
        siren.volume = 0f;
        siren.pitch = normalPitch;
        pitchVelocity = 0f;
        siren.Play();
    }

    /// <summary>
    /// Winds the siren down and plays the HATER FREE! cinematic, then calls <paramref name="onFinished"/>.
    /// The vehicle is left in the scene the whole time.
    /// </summary>
    public void PlayDefeat(Action onFinished)
    {
        if (DefeatHandled) { onFinished?.Invoke(); return; }
        DefeatHandled = true;

        // Killed before it ever saw the player? Nothing to fade - just hold silence.
        if (state == State.Idle && siren != null) siren.volume = 0f;
        state = State.Defeated;
        pitchVelocity = 0f;

        // Don't fight the WASHED sequence for timeScale if the player died on the same frame.
        if (PimlrDeathSequence.IsPlaying) { onFinished?.Invoke(); return; }

        PimlrVictorySequence.Play(defeatWord, defeatColor, onFinished);
    }

    void Update()
    {
        if (state == State.Idle)
        {
            // Pursuit starts when the AI has a target and is actually driving.
            if (ai != null && ai.enabled && ai.targetChase != null && (health == null || !health.IsDead))
                BeginPursuit();
            return;
        }

        HandlePause();
        if (pausedByGame) return;

        // Real seconds, so the fade-out keeps its length inside the slow-motion cinematic.
        float dt = Time.unscaledDeltaTime;

        // ---- Volume ----
        float targetVolume = state == State.Pursuing ? maxVolume : 0f;
        float seconds = state == State.Pursuing ? fadeInSeconds : fadeOutSeconds;
        float step = seconds > 0.01f ? (maxVolume / seconds) * dt : 1f;
        siren.volume = Mathf.MoveTowards(siren.volume, targetVolume, step);

        // ---- Pitch ----
        float targetPitch = normalPitch;
        float smooth = pitchSmoothSeconds;
        if (state == State.Defeated)
        {
            targetPitch = defeatPitch;
            smooth = defeatPitchSmoothSeconds;
        }
        else if (health != null && health.Health < lowHealthThreshold)
        {
            float lost = 1f - Mathf.Clamp01(health.Health / lowHealthThreshold); // 0 at 75 HP -> 1 at 0 HP
            targetPitch = Mathf.Lerp(normalPitch, minPitch, lost);
        }
        siren.pitch = Mathf.SmoothDamp(siren.pitch, targetPitch, ref pitchVelocity, smooth, Mathf.Infinity, dt);

        if (state == State.Defeated && siren.volume <= 0.001f && siren.isPlaying)
            siren.Stop();
    }

    void HandlePause()
    {
        bool paused = JUPauseGame.Paused;
        if (paused == pausedByGame) return;
        pausedByGame = paused;
        if (paused) siren.Pause(); else siren.UnPause();
    }
}