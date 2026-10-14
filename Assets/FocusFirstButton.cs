using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FocusFirstButton : MonoBehaviour
{
    [SerializeField] Selectable firstSelected;
    void OnEnable() => EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
}