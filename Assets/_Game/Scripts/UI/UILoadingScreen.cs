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
        string sliderDetails = loadingSlider != null
            ? $"{loadingSlider.name} range={loadingSlider.minValue:F2}-{loadingSlider.maxValue:F2}, normalized={loadingSlider.normalizedValue:F2}, active={loadingSlider.gameObject.activeInHierarchy}"
            : "NULL";
        Debug.Log($"[UILodingScreen:{GetInstanceID()}] Enabled on '{gameObject.scene.name}/{name}'. Slider={sliderDetails}, text={(loadingText != null ? loadingText.name : "NULL")}.");

        if (AuthManager.Instance == null || loadingBarDatas == null)
        {
            Debug.LogWarning($"[UILodingScreen:{GetInstanceID()}] Cannot populate loading-screen title/description: AuthManager={(AuthManager.Instance != null ? "available" : "NULL")}, loadingBarDatas={(loadingBarDatas != null ? loadingBarDatas.Length.ToString() : "NULL")}.");
            return;
        }

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

