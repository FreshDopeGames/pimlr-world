using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// PIMLR: created automatically at startup (no scene/prefab setup needed). Each frame it tracks which device the
/// player is using; PimlrMenuNavigator (same object) handles controller menu navigation. On every scene load it applies player feel tuning and
/// installs the controller-aware Controls Info panel and vehicle interact prompts.
/// </summary>
public class PimlrInputRuntime : MonoBehaviour
{
    static PimlrInputRuntime instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance) return;
        var go = new GameObject("PIMLR_Input");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PimlrInputRuntime>();
        go.AddComponent<PimlrMenuNavigator>();
        go.AddComponent<PimlrVehiclePrompt>(); // floating "F to Drive" / "A to Ride"
    }

    void Awake()
    {
        PimlrInput.SetUsingGamepad(Gamepad.current != null, force: true);
        SceneManager.sceneLoaded += OnSceneLoaded;
        InputSystem.onDeviceChange += OnDeviceChange;
        StartCoroutine(InstallForScene());
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(InstallForScene());

    IEnumerator InstallForScene()
    {
        yield return null; // let the scene's own Start() methods run first
        PimlrPlayerFeel.ApplyToPlayers();
        PimlrControlsInfo.InstallAll();
        PimlrVehicleInteractZone.InstallAll();
        PimlrHomeButtons.InstallAll();
        PimlrCoinLabel.InstallAll();
        PimlrUISpriteFix.ConvertAll();
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!(device is Gamepad)) return;
        if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
        {
            if (Gamepad.current == null) PimlrInput.SetUsingGamepad(false);
        }
        else if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
        {
            PimlrInput.SetUsingGamepad(true, force: true);
        }
    }

    void Update()
    {
        DetectScheme();
    }

    static void DetectScheme()
    {
        var pad = Gamepad.current;
        bool padUsed = pad != null && (
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
            pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
            pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
            pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
            pad.leftStickButton.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame ||
            pad.dpad.ReadValue().sqrMagnitude > 0.25f ||
            pad.leftStick.ReadValue().magnitude > 0.4f || pad.rightStick.ReadValue().magnitude > 0.4f ||
            pad.leftTrigger.ReadValue() > 0.3f || pad.rightTrigger.ReadValue() > 0.3f);

        bool kbmUsed =
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame ||
                                       Mouse.current.rightButton.wasPressedThisFrame ||
                                       Mouse.current.delta.ReadValue().sqrMagnitude > 25f));

        if (padUsed) PimlrInput.SetUsingGamepad(true);
        else if (kbmUsed) PimlrInput.SetUsingGamepad(false);
        else if (PimlrInput.UsingGamepad) PimlrInput.SetUsingGamepad(true); // picks up Xbox <-> PlayStation swaps
    }
}
