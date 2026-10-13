using JUTPS.ActionScripts;
using JUTPS.VehicleSystem;
using TMPro;
using UnityEngine;

/// <summary>
/// PIMLR: added at runtime to every vehicle "VehicleArea" trigger. Widens the trigger so the player can
/// get in from a bit further away, and shows a camera-facing prompt above the vehicle while the player is in
/// range, worded for the active controls ("F to Ride", "A to Drive", "Cross to Ride").
/// </summary>
public class PimlrVehicleInteractZone : MonoBehaviour
{
    public const float ExtraReach = 1.5f;      // metres added around the original trigger
    public const float PromptHeightPad = 0.45f;

    public Vehicle vehicle;
    public string verb = "Ride";

    Collider zone;
    TextMeshPro label;
    float heightAboveRoot = 2f;
    string lastText;

    static DriveVehicles player;
    static Camera cam;
    static bool loggedFirstShow;

    // JUTPS's camera isn't always tagged MainCamera; without a camera the text never turns to face you and,
    // being one-sided, is invisible from most angles.
    static Camera FindGameplayCamera()
    {
        Camera main = Camera.main;
        if (main && main.isActiveAndEnabled) return main;
        Camera best = null;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
        return best;
    }

    void OnDestroy()
    {
        if (label) Destroy(label.gameObject);
    }

    public static void InstallAll()
    {
        player = null;
        cam = null;
        loggedFirstShow = false;
        foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!col.isTrigger || !col.CompareTag("VehicleArea")) continue;
            if (col.GetComponent<PimlrVehicleInteractZone>()) continue;
            col.gameObject.AddComponent<PimlrVehicleInteractZone>().Init(col);
        }
    }

    void Init(Collider col)
    {
        zone = col;
        vehicle = col.GetComponentInParent<Vehicle>();
        verb = vehicle is CarController ? "Drive" : "Ride";
        Widen(col);

        // Height of the vehicle's visuals above its root, for the prompt.
        Transform root = vehicle ? vehicle.transform : col.transform;
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
            heightAboveRoot = b.max.y - root.position.y + PromptHeightPad;
        }
        heightAboveRoot = Mathf.Clamp(heightAboveRoot, 1.2f, 4f); // ignore stray giant/flat renderers

        // Unparented on purpose: vehicle trigger objects are often scaled (or scaled to ~0), which shrank or hid
        // the text. A root object with scale 1 always reads the same size.
        var go = new GameObject("PIMLR_InteractPrompt (" + (vehicle ? vehicle.name : name) + ")");
        go.layer = 0; // Default - always rendered by the gameplay camera
        label = go.AddComponent<TextMeshPro>();
        if (label.font == null && TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        label.enableWordWrapping = false;
        label.fontSize = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        label.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
        label.enabled = false;
    }

    static void Widen(Collider col)
    {
        Vector3 s = col.transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(s.x)), sz = Mathf.Max(0.0001f, Mathf.Abs(s.z));
        switch (col)
        {
            case BoxCollider box:
                box.size += new Vector3(2f * ExtraReach / sx, 0f, 2f * ExtraReach / sz);
                break;
            case SphereCollider sphere:
                sphere.radius += ExtraReach / Mathf.Max(sx, sz);
                break;
            case CapsuleCollider capsule:
                capsule.radius += ExtraReach / Mathf.Max(sx, sz);
                break;
        }
    }

    void LateUpdate()
    {
        if (!label) return;

        if (!player)
        {
            var p = GameObject.FindWithTag("Player");
            player = p ? p.GetComponent<DriveVehicles>() : null;
        }
        if (!cam || !cam.isActiveAndEnabled) cam = FindGameplayCamera();

        bool show = player && vehicle && player.VehicleDrivableNearby && player.VehicleToDrive == vehicle
                    && !player.IsDriving && !player.IsDrivingByShoot;

        if (label.enabled != show)
        {
            label.enabled = show;
            if (show && !loggedFirstShow)
            {
                loggedFirstShow = true;
                Debug.Log($"PIMLR: vehicle prompt shown for {vehicle.name} (camera: {(cam ? cam.name : "none")}).", this);
            }
        }
        if (!show) return;

        PimlrInput.MarkInteractPromptVisible();

        string text = $"{PimlrInput.Label(PimlrButton.Interact)} to {verb}";
        if (text != lastText) { label.text = text; lastText = text; }

        Transform root = vehicle.transform;
        label.transform.position = root.position + Vector3.up * heightAboveRoot;
        if (cam) label.transform.rotation = cam.transform.rotation;
    }
}
