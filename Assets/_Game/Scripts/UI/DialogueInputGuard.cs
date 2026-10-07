using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// PIMLR (playtest): keeps Sirihanna's world-space Dialogue_Canvas clickable and soft-lock proof.
// Put it on the Dialogue_Canvas object, next to ChatbotUIController.
[RequireComponent(typeof(Canvas))]
public class DialogueInputGuard : MonoBehaviour
{
    [SerializeField] private ChatbotUIController chat;
    [SerializeField] private bool logClickProbe = true;

    private Canvas canvas;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        if (chat == null) chat = GetComponent<ChatbotUIController>();
    }

    private void OnEnable()
    {
        EnsureEventSystem();
        if (canvas.worldCamera == null || !canvas.worldCamera.isActiveAndEnabled)
            canvas.worldCamera = Camera.main;
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        Debug.Log("[DialogueInputGuard] EventSystem=" + (EventSystem.current != null ? EventSystem.current.name : "NONE")
            + " worldCamera=" + (canvas.worldCamera != null ? canvas.worldCamera.name : "NONE"));
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject go = new GameObject("EventSystem (DialogueInputGuard)");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Debug.LogWarning("[DialogueInputGuard] No EventSystem existed, created one. Fix the scene/UI setup so this is not needed.");
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (logClickProbe && Input.GetMouseButtonDown(0) && EventSystem.current != null)
        {
            PointerEventData data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            Debug.Log("[DialogueInputGuard] click hits=" + hits.Count
                + (hits.Count > 0 ? " top=" + hits[0].gameObject.name : " top=NOTHING")
                + " cursorLock=" + Cursor.lockState + " visible=" + Cursor.visible);
        }
        // Keyboard fallback so a click problem can never trap the story: N = Next. (Left/Right Ctrl already closes the panel.)
        if (chat != null && Input.GetKeyDown(KeyCode.N)) chat.AdvanceDialogue();
#endif
    }

    private void LateUpdate()
    {
        // JUTPS can re-lock the cursor while a panel is open.
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
    }
}