using UnityEngine;
using UnityEngine.EventSystems;

public class NavDebug : MonoBehaviour
{
    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(Vector3.one * 1.5f);
        GUILayout.BeginArea(new Rect(10, 10, 700, 200), GUI.skin.box);
        var es = EventSystem.current;
        GUILayout.Label("EventSystem: " + (es ? "yes" : "NONE") +
                        "   selected: " + (es && es.currentSelectedGameObject ? es.currentSelectedGameObject.name : "none"));
        GUILayout.Label("UsingGamepad=" + PimlrInput.UsingGamepad + "  Cursor.visible=" + Cursor.visible +
                        "  IsNavigating=" + PimlrMenuNavigator.IsNavigating + "  Camera.main=" + (Camera.main != null));
        GUILayout.EndArea();
    }
}