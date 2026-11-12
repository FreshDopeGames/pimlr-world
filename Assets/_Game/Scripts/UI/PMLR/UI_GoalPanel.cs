
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UI_GoalPanel : MonoBehaviour
{

    public List<GoldData> goalLists;


    /// <summary>
    /// Kill Info
    /// </summary>
    public GameObject enemyInfoHolder;
    public TextMeshProUGUI currentEnemyInfoData;


    /// <summary>
    /// Booter Effect
    /// </summary>
    public Image boosterPanel;
    public GameObject boosterPanelHolder;
    public TMP_Text boosterTitleTxt; // PIMLR: TMP (Barlow-Bold SDF), was legacy Text
    public TMP_Text boosterInfoTxt;





    private void Start()
    {
        SceneManagerScript.Instance.goalPanel = this;
        ResetGoal();
    }



    private void OnDisable()
    {
        AuthManager.Instance.PowerChange(AchievementReward.Nothing);
    }

    private void OnDestroy()
    {
        AuthManager.Instance.PowerChange(AchievementReward.Nothing);

    }
    public void StartBoost(string title, string info, AchievementReward achievementReward)
    {
        AuthManager.Instance.PowerChange(achievementReward);
        boosterPanel.gameObject.SetActive(false);
        boosterPanelHolder.transform.DOPause();
        boosterPanel.transform.DOPause();
        boosterPanel.DOFade(0, 0);
        if (!string.IsNullOrEmpty(title))
        {

            boosterPanelHolder.transform.localScale = Vector3.zero;
            boosterTitleTxt.text = title;
            boosterInfoTxt.text = info;
            boosterPanel.gameObject.SetActive(true);
            boosterPanel.DOFade(0.35f, 0.35f).SetDelay(0.2f).OnComplete(() =>
            {
                boosterPanel.DOFade(0f, 0.2f).SetDelay(0.2f);
                boosterPanel.DOFade(0.35f, 0.35f).SetDelay(0.45f).OnComplete(() =>
                {
                    boosterPanel.DOFade(0f, 0.2f).SetDelay(0.2f);
                });
            });

            boosterPanelHolder.transform.DOScale(1, 0.5f).SetEase(Ease.OutBounce).SetDelay(0.5f).OnComplete(() =>
             {
                 boosterPanelHolder.transform.DOScale(0, 0.5f).SetEase(Ease.OutBounce).SetDelay(0.5f).OnComplete(() =>
                 {
                     boosterPanel.gameObject.SetActive(false);
                 });
             });
        }
    }

    public void OnCompleteGoal(GoalList completegoal)
    {
        for (int i = 0; i < goalLists.Count; i++)
        {
            goalLists[i].toggle.isOn = true;
            if (goalLists[i].goal == completegoal)
            {
                return;
            }
        }
        //Debug.Log("OnCompleteGoal:::" + completegoal.ToString());

    }

    public void SetCurrentKillInfo(string infoData)
    {

        //Debug.Log("SetCurrentKillInfo:::" + infoData);
        enemyInfoHolder.SetActive(true);
        currentEnemyInfoData.text = infoData;
    }

    public void ResetGoal()
    {
        for (int i = 0; i < goalLists.Count; i++)
        {

            goalLists[i].toggle.isOn = false;

        }

        enemyInfoHolder.SetActive(false);
        currentEnemyInfoData.text = "0 / 0";
    }


}

[System.Serializable]
public class GoldData
{
    public GoalList goal;
    public Toggle toggle;
}
public enum GoalList
{
    ChatWithSirihanna,
    Zone1_Enemy_Battle,
    Zone1_Boss_Battle,
    Zone2_Enemy_Battle,
    EnemyCarDestroy
}