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
        {
            Debug.LogWarning($"[UILodingScreen:{GetInstanceID()}] Cannot populate loading-screen title/description: AuthManager={(AuthManager.Instance != null ? "available" : "NULL")}, loadingBarDatas={(loadingBarDatas != null ? loadingBarDatas.Length.ToString() : "NULL")}.");
            return;
        }

        // PIMLR (playtest): loading copy is shared and always comes from the first configured entry.
        if (loadingBarDatas.Length == 0 || loadingBarDatas[0] == null)
            return;

        if (titleText != null)
            titleText.text = loadingBarDatas[0]._Titel;
        if (descriptionText != null)
            descriptionText.text = loadingBarDatas[0]._Description;
    }

    public void ShowProgress()
    {
        if (loadingSlider != null) loadingSlider.gameObject.SetActive(true);
        if (loadingText != null) loadingText.gameObject.SetActive(true);
        foreach (Button b in GetComponentsInChildren<Button>(true))
            if (b.name == "NewGame Button" || b.name == "KeepPlaying Button")
                b.gameObject.SetActive(false);
    }

}

[System.Serializable]

public class LoadingBarData
{
    public string _Titel;
    public string _Description;
}
