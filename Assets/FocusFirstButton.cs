using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FocusFirstButton : MonoBehaviour
{
    [SerializeField] Selectable firstSelected;
    // PIMLR (Nov 18): null-safe - no EventSystem / no target (e.g. the frame the artist-select canvas closes) threw here.
    void OnEnable()
    {
        if (EventSystem.current != null && firstSelected != null) EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
    }
}