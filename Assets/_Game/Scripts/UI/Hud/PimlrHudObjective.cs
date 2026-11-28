// #UI Slice 4b: one objective at a time (Craig, Nov 27: "singularly-focused to avoid cluttering the HUD").
// Sits on the existing "Goal Panel" (UI_GoalPanel). Its rows stay in the prefab, and UI_GoalPanel keeps ticking their
// toggles exactly as before; this only shows the first row whose toggle isn't on yet, with a green diamond marker
// (theme slot ObjectiveMarker when set). Every objective done = the panel fades out (it is never deactivated:
// UI_GoalPanel.OnDisable would end the active boost).
// (A full objectives list belongs on the Pause screen: Slice 5.)
//
// Unreal note: WBP_CurrentObjective bound to the objective subsystem's CurrentObjectiveChanged delegate.

using PIMLR.UI;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(UI_GoalPanel))]
public class PimlrHudObjective : MonoBehaviour
{
    UI_GoalPanel goals;
    CanvasGroup group;
    int shown = -2;

    void Awake()
    {
        goals = GetComponent<UI_GoalPanel>();
        group = GetComponent<CanvasGroup>(); if (!group) group = gameObject.AddComponent<CanvasGroup>(); // (no ?? on Unity objects)
        group.interactable = false; group.blocksRaycasts = false;
    }

    void OnEnable() { shown = -2; Refresh(); }

    void LateUpdate() => Refresh();

    void Refresh()
    {
        if (!goals || goals.goalLists == null) return;
        int current = -1;
        for (int i = 0; i < goals.goalLists.Count; i++)
        {
            var t = goals.goalLists[i].toggle;
            if (t && !t.isOn) { current = i; break; }
        }
        group.alpha = current >= 0 ? 1f : 0f;
        if (current == shown) return;
        shown = current;
        for (int i = 0; i < goals.goalLists.Count; i++)
        {
            var t = goals.goalLists[i].toggle;
            if (!t) continue;
            var row = t.transform.parent != transform ? t.transform.parent : t.transform;
            row.gameObject.SetActive(i == current);
        }
        var rt = (RectTransform)transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }
}
