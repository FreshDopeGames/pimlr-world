using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NextButtonProbe : MonoBehaviour
{
    [SerializeField] Selectable next;
    readonly List<RaycastResult> hits = new List<RaycastResult>();
    string report = "";

    void Update()
    {
        if (next == null || EventSystem.current == null) { report = "assign Next / no EventSystem"; return; }
        var es = EventSystem.current;
        var canvas = next.GetComponentInParent<Canvas>();
        var root = canvas ? canvas.rootCanvas : null;
        Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay
            ? (root.worldCamera ? root.worldCamera : Camera.main) : null;
        var rt = (RectTransform)next.transform;
        Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));

        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = p }, hits);
        string top = hits.Count > 0 ? hits[0].gameObject.name : "nothing";
        bool topIsNext = hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(next.transform);
        next.TryGetComponent(out PimlrNavHint hint);

        report =
            $"type={next.GetType().Name} active={next.isActiveAndEnabled} interactable={next.IsInteractable()}\n" +
            $"navHint={(hint ? $"skip={hint.skip}" : "none")} canvasMode={(root ? root.renderMode.ToString() : "none")} cam={(cam ? cam.name : "null")}\n" +
            $"screenPos={p} onScreen={(p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height)}\n" +
            $"topRaycastHit={top}  hitsNext={topIsNext}";
    }

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(Vector3.one * 1.4f);
        GUI.Label(new Rect(10, 120, 900, 120), report, GUI.skin.box);
    }
}