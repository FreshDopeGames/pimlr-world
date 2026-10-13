using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MusicSystem : MonoBehaviour
{
    [Header("AudioSection")]
    public AudioSource musicSystem;
    public List<AudioClip> audioList = new List<AudioClip>();
    public bool music;
    public string songName;
    public TextMeshProUGUI songNameTXT;
    public TextMeshProUGUI songNameTXT_2;
    public Slider musicSlider, musicSlider2;
    public GameObject idleSystem, activeSystem;
    public GameObject idleSystem2, activeSystem2;

    // PIMLR: one player panel for playing AND paused (the ActiveSystem layout). The Idle panels are kept hidden;
    // the play/pause button swaps its icon and the disc stops spinning while paused.
    [Header("PIMLR single-panel player")]
    [Tooltip("Icon shown on play/pause while paused. Auto-taken from the Idle panel's play/pause button if empty.")]
    public Sprite playIcon;
    [Tooltip("Icon shown on play/pause while playing. Auto-taken from the ActiveSystem panel's play/pause button if empty.")]
    public Sprite pauseIcon;
    bool panelsPrepared;

    public Color selectedRepeatedMode;
    public Image repeatedModelBtn;
    bool isRepetedMusic;


    public TextMeshProUGUI currentClipTime;


    bool startMusic;
    private void OnEnable()
    {
        if (SceneManager.GetActiveScene().name != "YannicksWorld")
        {
            musicSystem.time = Mathf.Clamp(currentTime, 0f, musicSystem.clip.length);
            if (startMusic && music)
                musicSystem.Play();
        }
    }



    // Start is called before the first frame update
    IEnumerator Start()
    {
        PreparePanels();
        ApplyPanelState();
        SceneManagerScript.Instance.musicSystem = this;
        MusicData[] musicData = SceneManagerScript.Instance.musicData;
        audioList = new List<AudioClip>();
        for (int i = 0; i < musicData.Length; i++)
        {
            if (musicData[i].isPurchased)
            {
                audioList.Add(musicData[i].audioClip);
            }
        }
        yield return new WaitForSeconds(1f);

        startMusic = true;
        if (SceneManager.GetActiveScene().name != "YannicksWorld")
        {
            music = false;
            PlayMusic();
        }
    }

    public void RefreshList()
    {
        MusicData[] musicData = SceneManagerScript.Instance.musicData;
        audioList = new List<AudioClip>();
        for (int i = 0; i < musicData.Length; i++)
        {
            if (musicData[i].isPurchased)
            {
                audioList.Add(musicData[i].audioClip);
            }
        }
    }


    // Update is called once per frame
    void Update()
    {
        songNameTXT.text = songName;
        if (songNameTXT_2)
            songNameTXT_2.text = songName;
        musicSlider.maxValue = musicSystem.clip.length;
        musicSlider.value = musicSystem.time;

        if (musicSlider2)
        {
            musicSlider2.maxValue = musicSystem.clip.length;
            musicSlider2.value = musicSystem.time;
        }
        if (currentClipTime)
            currentClipTime.text = TimeSpan.FromSeconds(musicSystem.time).Minutes.ToString("00") + ":" + TimeSpan.FromSeconds(musicSystem.time).Seconds.ToString("00");
        //Debug.Log("musicSlider.value: " + musicSlider.value);
    }
    private void OnDisable()
    {
        currentTime = musicSystem.time;
    }
    #region AudioSection

    float currentTime = 0;
    public void PlayMusic()
    {
        Debug.Log("PlayMusic::::>" + music);
        if (music)
        {
            Debug.Log("isStoping" + musicSystem.time);
            musicSystem.Pause();
            currentTime = musicSystem.time;
            music = false;
            ApplyPanelState(); // PIMLR: same panel, play icon, disc stops

            // PIMLR #14: added "&& SceneManagerScript.Instance.goalPanel != null" — scenes like SceneStaticEU have no UI_GoalPanel.
            if (SceneManagerScript.Instance != null && SceneManager.GetActiveScene().name != "00_MainMenu" && SceneManagerScript.Instance.goalPanel != null)
            {

                AuthManager.Instance.PowerChange(AchievementReward.Nothing);
                // PIMLR #4: also hide the on-screen boost panel when music is paused/stopped.
                if (SceneManagerScript.Instance.goalPanel.boosterPanel != null)
                    SceneManagerScript.Instance.goalPanel.boosterPanel.gameObject.SetActive(false);
            }
        }
        else
        {
            // PIMLR #14: this line previously had no guard at all — added the full scene/goalPanel check.
            if (SceneManagerScript.Instance != null && SceneManager.GetActiveScene().name != "00_MainMenu" && SceneManagerScript.Instance.goalPanel != null && SceneManagerScript.Instance.goalPanel.boosterPanel != null)
                SceneManagerScript.Instance.goalPanel.boosterPanel.gameObject.SetActive(false);
            Debug.Log("isPlaying:::>>" + currentTime);
            musicSystem.time = Mathf.Clamp(currentTime, 0f, musicSystem.clip.length);
            musicSystem.Play();
            songName = musicSystem.clip.name;
            music = true;
            ApplyPanelState(); // PIMLR: same panel, pause icon, disc spins

            // PIMLR #14: added "&& SceneManagerScript.Instance.goalPanel != null".
            if (SceneManagerScript.Instance != null && SceneManager.GetActiveScene().name != "00_MainMenu" && SceneManagerScript.Instance.goalPanel != null)
            {
                for (int i = 0; i < SceneManagerScript.Instance.musicData.Length; i++)
                {
                    //Debug.Log(musicSystem.clip.name + "Music changes" + SceneManagerScript.Instance.musicData[i].audioClip.name);
                    if (SceneManagerScript.Instance.musicData[i].audioClip.name == musicSystem.clip.name)
                    {
                        //Debug.Log("Music changes::>");
                        SceneManagerScript.Instance.goalPanel.StartBoost(SceneManagerScript.Instance.musicData[i].boostname, SceneManagerScript.Instance.musicData[i].boostInfo, SceneManagerScript.Instance.musicData[i].achievementReward);

                    }
                }
            }
        }

    }

    // ---------- PIMLR single-panel player ----------

    void PreparePanels()
    {
        if (panelsPrepared) return;
        panelsPrepared = true;
        if (!playIcon) playIcon = IconOf(idleSystem) ? IconOf(idleSystem) : IconOf(idleSystem2);
        if (!pauseIcon) pauseIcon = IconOf(activeSystem) ? IconOf(activeSystem) : IconOf(activeSystem2);
    }

    /// <summary>Shows the one panel per player and sets the play/pause icon + disc spin for the current state.</summary>
    void ApplyPanelState()
    {
        PreparePanels();
        ApplyPanel(activeSystem, idleSystem);
        ApplyPanel(activeSystem2, idleSystem2);
    }

    void ApplyPanel(GameObject panel, GameObject idle)
    {
        // No ActiveSystem assigned for this player: fall back to using its Idle panel as the single panel.
        GameObject shown = panel ? panel : idle;
        if (!shown) return;
        if (idle && idle != shown) idle.SetActive(false);
        shown.SetActive(true);

        var button = FindPlayPauseImage(shown);
        if (button)
        {
            Sprite icon = music ? pauseIcon : playIcon;
            if (icon) { button.sprite = icon; button.preserveAspect = true; }
        }

        foreach (var rotator in shown.GetComponentsInChildren<JUTPS.Utilities.JUAutoRotator>(true))
            rotator.enabled = music;
    }

    static Sprite IconOf(GameObject panel)
    {
        var img = FindPlayPauseImage(panel);
        return img != null ? img.sprite : null;
    }

    static Image FindPlayPauseImage(GameObject panel)
    {
        if (!panel) return null;
        foreach (var t in panel.GetComponentsInChildren<Transform>(true))
            if (t.name == "play/pause" && t.TryGetComponent(out Image img)) return img;
        return null;
    }


    public void OnMusicShopClick()
    {
        if (SceneManagerScript.Instance && SceneManagerScript.Instance.musicPurchasePanel)
        {
            SceneManagerScript.Instance.musicPurchasePanel.gameObject.SetActive(true);
        }
    }

    int currentSoundClipCount = 0;
    public void ChangeMusicForward()
    {
        // PIMLR #14: this line previously had no guard at all.
        if (SceneManagerScript.Instance != null && SceneManagerScript.Instance.goalPanel != null && SceneManagerScript.Instance.goalPanel.boosterPanel != null)
            SceneManagerScript.Instance.goalPanel.boosterPanel.gameObject.SetActive(false);
        // PIMLR #4: clear the previous track's attribute boost before applying the new one.
        AuthManager.Instance.PowerChange(AchievementReward.Nothing);
        currentSoundClipCount++;
        if (currentSoundClipCount >= audioList.Count)
        {
            currentSoundClipCount = 0;
        }
        musicSystem.clip = audioList[currentSoundClipCount];
        songName = musicSystem.clip.name;
        //Debug.Log("  " + musicSystem.clip.length);
        musicSystem.Play();
        music = true; ApplyPanelState(); // PIMLR: skipping tracks starts playback - show the pause icon / spin
        // PIMLR #14: added "&& SceneManagerScript.Instance.goalPanel != null".
        if (SceneManagerScript.Instance != null && SceneManager.GetActiveScene().name != "00_MainMenu" && SceneManagerScript.Instance.goalPanel != null)
        {
            for (int i = 0; i < SceneManagerScript.Instance.musicData.Length; i++)
            {
                if (SceneManagerScript.Instance.musicData[i].audioClip == musicSystem.clip)
                {
                    SceneManagerScript.Instance.goalPanel.StartBoost(SceneManagerScript.Instance.musicData[i].boostname, SceneManagerScript.Instance.musicData[i].boostInfo, SceneManagerScript.Instance.musicData[i].achievementReward);
                }
            }
        }

    }

    public void ChangeMusic(string clipname)
    {
        // PIMLR #14: this line previously had no guard at all.
        if (SceneManagerScript.Instance != null && SceneManagerScript.Instance.goalPanel != null && SceneManagerScript.Instance.goalPanel.boosterPanel != null)
            SceneManagerScript.Instance.goalPanel.boosterPanel.gameObject.SetActive(false);
        // PIMLR #4: clear the previous track's attribute boost before applying the new one.
        AuthManager.Instance.PowerChange(AchievementReward.Nothing);

        for (int i = 0; i < audioList.Count; i++)
        {
            if(audioList[i].name.Equals(clipname))
            {
                currentSoundClipCount = i;
                break;
            }
        }

        musicSystem.clip = audioList[currentSoundClipCount];
        songName = musicSystem.clip.name;
        //Debug.Log("  " + musicSystem.clip.length);
        musicSystem.Play();
        music = true; ApplyPanelState(); // PIMLR: skipping tracks starts playback - show the pause icon / spin
        // PIMLR #14: added "&& SceneManagerScript.Instance.goalPanel != null".
        if (SceneManagerScript.Instance != null && SceneManager.GetActiveScene().name != "00_MainMenu" && SceneManagerScript.Instance.goalPanel != null)
        {
            for (int i = 0; i < SceneManagerScript.Instance.musicData.Length; i++)
            {
                if (SceneManagerScript.Instance.musicData[i].audioClip == musicSystem.clip)
                {
                    SceneManagerScript.Instance.goalPanel.StartBoost(SceneManagerScript.Instance.musicData[i].boostname, SceneManagerScript.Instance.musicData[i].boostInfo, SceneManagerScript.Instance.musicData[i].achievementReward);
                }
            }
        }

    }

    public void ChangeMusicBackward()
    {
        // PIMLR #14: this line previously had no guard at all.
        if (SceneManagerScript.Instance != null && SceneManagerScript.Instance.goalPanel != null && SceneManagerScript.Instance.goalPanel.boosterPanel != null)
            SceneManagerScript.Instance.goalPanel.boosterPanel.gameObject.SetActive(false);
        // PIMLR #4: clear the previous track's attribute boost before applying the new one.
        AuthManager.Instance.PowerChange(AchievementReward.Nothing);
        currentSoundClipCount--;
        if (currentSoundClipCount < 0)
        {
            currentSoundClipCount = audioList.Count - 1;
        }
        musicSystem.clip = audioList[currentSoundClipCount];
        songName = musicSystem.clip.name;
        musicSystem.Play();
        music = true; ApplyPanelState(); // PIMLR: skipping tracks starts playback - show the pause icon / spin
        // PIMLR #14: added "&& SceneManagerScript.Instance.goalPanel != null".
        if (SceneManagerScript.Instance != null && SceneManager.GetActiveScene().name != "00_MainMenu" && SceneManagerScript.Instance.goalPanel != null)
        {
            for (int i = 0; i < SceneManagerScript.Instance.musicData.Length; i++)
            {
                if (SceneManagerScript.Instance.musicData[i].audioClip == musicSystem.clip)
                {
                    SceneManagerScript.Instance.goalPanel.StartBoost(SceneManagerScript.Instance.musicData[i].boostname, SceneManagerScript.Instance.musicData[i].boostInfo, SceneManagerScript.Instance.musicData[i].achievementReward);
                }
            }
        }
    }

    public void OnShuffleBtnClick()
    {
        Shuffle(audioList);

        currentSoundClipCount = 0;
        if (musicSystem.isPlaying)
        {
            currentSoundClipCount = audioList.Count + 1;
            ChangeMusicForward();
        }
    }
    void Shuffle(List<AudioClip> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            AudioClip value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
    public void OnRepeateBtnClick()
    {
        isRepetedMusic = !isRepetedMusic;
        if (isRepetedMusic)
        {
            musicSystem.loop = true;
            repeatedModelBtn.color = selectedRepeatedMode;
        }
        else
        {
            musicSystem.loop = false;
            repeatedModelBtn.color = Color.white;
        }
    }
    #endregion
}
