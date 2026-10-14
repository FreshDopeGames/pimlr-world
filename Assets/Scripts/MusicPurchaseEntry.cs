
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MusicPurchaseEntry : MonoBehaviour
{

    public int coinValue;
    public Image albumIcon;
    public TextMeshProUGUI songTitle;
    public TextMeshProUGUI songDescription;
    public GameObject purchaseBtn, alreadyPurhcaseBtn, lockedButton;
    public TextMeshProUGUI lockBtnText;
    public TextMeshProUGUI musicValueTxt;
    public AudioClip audioClip;
    MusicData storeMusicData;
    public void OnAssignData(MusicData musicData)
    {
        storeMusicData = musicData;
        coinValue = musicData.musicValue;
        // PIMLR: the song's Album Icon (SceneManagerScript > Music Data) overrides the prefab's Icon image.
        // Leave it empty to keep whatever art the MusicPurchaseEntry prefab shows.
        if (musicData.albumIcon != null) albumIcon.sprite = musicData.albumIcon;
        songTitle.text = musicData.audioClip.name;
        songDescription.text = musicData.songDescription;
        purchaseBtn.SetActive(!musicData.isPurchased);
        alreadyPurhcaseBtn.SetActive(musicData.isPurchased);
        lockedButton.SetActive(!musicData.isPurchased && !musicData.isFree);
        audioClip = musicData.audioClip;
        lockBtnText.text = musicData.lockedBtnMsg;
        musicValueTxt.text = musicData.musicValue.ToString();
        OnEnable();
    }

    private void OnEnable()
    {
        if (storeMusicData != null)
        {
            if (!storeMusicData.isPurchased && !storeMusicData.isFree)
            {
                if (storeMusicData.achievementType == AchievementType.EnemyWash)
                {
                    lockedButton.SetActive(storeMusicData.totalTargetAmount > AuthManager.Instance.enemyWashValue);
                }
                else if (storeMusicData.achievementType == AchievementType.BossWash)
                {
                    lockedButton.SetActive(storeMusicData.totalTargetAmount > AuthManager.Instance.bossWashValue);
                }
                else if (storeMusicData.achievementType == AchievementType.PoliceCar)
                {
                    lockedButton.SetActive(storeMusicData.totalTargetAmount > AuthManager.Instance.policeCarValue);
                }

            }
            else
            {
                lockedButton.SetActive(false);
            }
        }

    }

    public void OnPurchaseBtnClick()
    {
        if (CoinManager.Instance && CoinManager.Instance.GetCoins() >= coinValue)
        {
            storeMusicData.isPurchased = true;
            CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() - coinValue);
            PlayerPrefs.SetInt(audioClip.name, 1);
            purchaseBtn.SetActive(false);
            alreadyPurhcaseBtn.SetActive(true);
            var ms = SceneManagerScript.Instance.musicSystem;
            if (ms != null) ms.audioList.Add(audioClip);
            SceneManagerScript.Instance.FetchMusicData();
        }
        else
        {
            SceneManagerScript.Instance.musicPurchasePanel.NotEnoughCoinMsg();
        }
    }

    public void OnPlayBtnClick()
    {
        SceneManagerScript.Instance.musicSystem.ChangeMusic(audioClip.name);
    }
}
