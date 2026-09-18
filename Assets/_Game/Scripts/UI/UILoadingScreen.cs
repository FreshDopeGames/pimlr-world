using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UILodingScreen : MonoBehaviour
{
    public LoadingBarData[] loadingBarDatas;
    public Image backGroundimg;
    public TMPro.TextMeshProUGUI titleText;
    public TMPro.TextMeshProUGUI descriptionText;
    public TMPro.TextMeshProUGUI loadingText;
    public Slider loadingSlider;

  

    private void OnEnable()
    {
        if (AuthManager.Instance == null || loadingBarDatas == null)
            return;

        int index = AuthManager.Instance.currentGameMode switch
        {
            GameMode.IdeaLabs => 0,
            GameMode.Pimlr => 1,
            GameMode.Helix => 2,
            GameMode.HumanityRocks => 3,
            _ => -1
        };
        if (index < 0 || index >= loadingBarDatas.Length || loadingBarDatas[index] == null)
            return;

        if (titleText != null)
            titleText.text = loadingBarDatas[index]._Titel;
        if (descriptionText != null)
            descriptionText.text = loadingBarDatas[index]._Description;
    }

}

[System.Serializable]

public class LoadingBarData
{
    public string _Titel;
    public string _Description;
}

