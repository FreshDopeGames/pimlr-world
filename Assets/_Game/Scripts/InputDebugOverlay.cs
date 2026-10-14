using UnityEngine;
using UnityEngine.InputSystem;

public class InputDebugOverlay : MonoBehaviour
{
    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(Vector3.one * 1.5f);
        GUILayout.BeginArea(new Rect(10, 10, 800, 300), GUI.skin.box);
        var p = Gamepad.current;
        GUILayout.Label(p == null ? "Gamepad.current: none" :
            $"type={p.GetType().Name} product={p.description.product}");
        GUILayout.Label($"IsPlayStation={PimlrInput.IsPlayStation}  UsingGamepad={PimlrInput.UsingGamepad}  InVehicle={PimlrInput.PlayerInVehicle}");
        GUILayout.Label($"MenuNavigator.IsNavigating={PimlrMenuNavigator.IsNavigating}");
        if (p != null) GUILayout.Label($"R2={p.rightTrigger.ReadValue():F2}  Cross={p.buttonSouth.isPressed}");
        GUILayout.EndArea();
    }
}