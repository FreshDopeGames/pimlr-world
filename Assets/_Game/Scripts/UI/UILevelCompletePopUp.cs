using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UILevelCompletePopUp : MonoBehaviour
{
    public LoadingBarData[] loadingBarDatas;
    public TMPro.TextMeshProUGUI titelText;
    public TMPro.TextMeshProUGUI descriptionText;

    public string infiniteModeDescription;

    private void OnEnable()
    {
        // PIMLR (playtest): select the zone copy once and guard each configured data slot.
        switch (GameExecutionManager.Instance.currentZoneMode)
        {
            case Zone.Zone1:
                if (loadingBarDatas != null && loadingBarDatas.Length > 0 && loadingBarDatas[0] != null)
                {
                    titelText.text = loadingBarDatas[0]._Titel;
                    descriptionText.text = loadingBarDatas[0]._Description;
                }
                break;
            case Zone.ZoneBoss1:
                if (loadingBarDatas != null && loadingBarDatas.Length > 1 && loadingBarDatas[1] != null)
                {
                    titelText.text = loadingBarDatas[1]._Titel;
                    descriptionText.text = loadingBarDatas[1]._Description;
                }
                break;
            case Zone.Zone2:
                if (loadingBarDatas != null && loadingBarDatas.Length > 2 && loadingBarDatas[2] != null)
                {
                    titelText.text = loadingBarDatas[2]._Titel;
                    descriptionText.text = loadingBarDatas[2]._Description;
                }
                break;
            case Zone.ZoneBoss2:
                if (loadingBarDatas != null && loadingBarDatas.Length > 3 && loadingBarDatas[3] != null)
                {
                    titelText.text = loadingBarDatas[3]._Titel;
                    descriptionText.text = loadingBarDatas[3]._Description;
                }
                break;
            case Zone.InfiniteMode:
                titelText.text = "<color=green>Wave "+ InfiniteMode.Instance.currentWave+ " begins</color>";
                descriptionText.text = infiniteModeDescription;
                break;
        }

    }
}
[System.Serializable]
public class LevelCompletePopUpData
{
    public string _Titel;
    public string _Description;
}

public enum Zone
{
    Zone1,
    ZoneBoss1,
    Zone2, 
    ZoneBoss2,
    ChatWilly,
    InfiniteMode
}
