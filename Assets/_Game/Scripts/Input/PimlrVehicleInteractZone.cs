using JUTPS.ActionScripts;
using JUTPS.VehicleSystem;
using TMPro;
using UnityEngine;

/// <summary>
/// PIMLR: added at runtime to every vehicle "VehicleArea" trigger to widen it, so the player can get in from a
/// bit further away. The floating "F to Drive" / "A to Ride" prompt is drawn by PimlrVehiclePrompt.
/// </summary>
public class PimlrVehicleInteractZone : MonoBehaviour
{
    public const float ExtraReach = 1.5f; // metres added around the original trigger

    public static void InstallAll()
    {
        int n = 0;
        foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!col.isTrigger) continue;
            if (!col.CompareTag("VehicleArea") && !col.CompareTag("VehicleShootingArea")) continue;
            if (col.GetComponent<PimlrVehicleInteractZone>()) continue;
            col.gameObject.AddComponent<PimlrVehicleInteractZone>();
            Widen(col);
            n++;
        }
        if (n > 0) Debug.Log($"PIMLR: widened {n} vehicle interact zone(s).");
    }

    static void Widen(Collider col)
    {
        Vector3 s = col.transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(s.x)), sz = Mathf.Max(0.0001f, Mathf.Abs(s.z));
        switch (col)
        {
            case BoxCollider box: box.size += new Vector3(2f * ExtraReach / sx, 0f, 2f * ExtraReach / sz); break;
            case SphereCollider sphere: sphere.radius += ExtraReach / Mathf.Max(sx, sz); break;
            case CapsuleCollider capsule: capsule.radius += ExtraReach / Mathf.Max(sx, sz); break;
        }
    }
}

/// <summary>
/// PIMLR: one camera-facing prompt above whichever vehicle the player can get into right now - read straight
/// from the player's DriveVehicles (the same state that decides whether the Interact button enters), so it can't
/// disagree with the game. Lives on the persistent PIMLR_Input object.
/// </summary>
public class PimlrVehiclePrompt : MonoBehaviour
{
    const float PadAboveRoof = 0.5f;

    TextMeshPro label;
    DriveVehicles player;
    Camera cam;
    Vehicle lastVehicle;
    float heightAboveRoot = 2f;
    string lastText;
    bool loggedShow;

    void LateUpdate()
    {
        if (!player)
        {
            var p = GameObject.FindWithTag("Player");
            player = p ? p.GetComponentInChildren<DriveVehicles>() : null;
            if (!player) { Hide(); return; }
        }

        Vehicle vehicle = null;
        string verb = "Drive";
        if (player.VehicleDrivableNearby && !player.IsDriving && !player.IsDrivingByShoot)
        {
            vehicle = player.VehicleToDrive ? player.VehicleToDrive : player.VehicleToShootWithDrive;
            if (vehicle && !(vehicle is CarController)) verb = "Ride";
        }
        if (!vehicle || !vehicle.gameObject.activeInHierarchy) { Hide(); return; }

        EnsureLabel();
        if (vehicle != lastVehicle) { lastVehicle = vehicle; heightAboveRoot = MeasureHeight(vehicle.transform); }

        PimlrInput.MarkInteractPromptVisible();
        string text = $"Press {PimlrInput.Label(PimlrButton.Interact)} To Ride";
        if (text != lastText) { label.text = text; lastText = text; }

        if (!cam || !cam.isActiveAndEnabled) cam = FindGameplayCamera();
        label.transform.position = vehicle.transform.position + Vector3.up * heightAboveRoot;
        if (cam) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, cam.transform.up);
        if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);

        if (!loggedShow)
        {
            loggedShow = true;
            Debug.Log($"PIMLR: vehicle prompt '{text}' shown over {vehicle.name} (camera: {(cam ? cam.name : "none")}, font: {(label.font ? label.font.name : "none")}).", vehicle);
        }
    }

    void Hide()
    {
        if (label && label.gameObject.activeSelf) label.gameObject.SetActive(false);
    }

    void EnsureLabel()
    {
        if (label) return;
        var go = new GameObject("PIMLR_VehiclePrompt");
        DontDestroyOnLoad(go);
        label = go.AddComponent<TextMeshPro>();

        // Use the same font the HUD uses (guaranteed to be in the build) rather than TMP's default lookup.
        var hudText = FindAnyObjectByType<TextMeshProUGUI>();
        if (hudText && hudText.font) label.font = hudText.font;
        else if (TMP_Settings.defaultFontAsset) label.font = TMP_Settings.defaultFontAsset;

        label.fontSize = 5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.color = Color.white;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        label.rectTransform.sizeDelta = new Vector2(10f, 2f);
        label.isOverlay = true; // draw over geometry so the vehicle itself can never hide the prompt
        go.SetActive(false);
    }

    static float MeasureHeight(Transform root)
    {
        bool any = false; Bounds b = default;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue;
            if (r.bounds.size.magnitude > 30f) continue; // not part of the vehicle body
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        // Only the vehicle body counts (skip huge child props / lights that would throw it off), and allow tall
        // vehicles - a 4 m cap put the prompt INSIDE the truck's cab, which is why only the bike showed it.
        float h = any ? b.max.y - root.position.y + PadAboveRoof : 2.5f;
        return Mathf.Clamp(h, 1.2f, 9f);
    }

    static Camera FindGameplayCamera()
    {
        Camera main = Camera.main;
        if (main && main.isActiveAndEnabled && main.targetTexture == null) return main;
        Camera best = null;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
        return best;
    }
}
