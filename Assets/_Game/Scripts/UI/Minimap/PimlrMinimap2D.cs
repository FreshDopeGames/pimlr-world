// PIMLR #MAP: flat 2D top-down minimap, player-up. Replaces the minimap camera's live render (a second full
// render of the city every frame, at 2048x2048) with a baked map image that slides and turns under the player.
//
// How it fits the existing minimap without touching the UI prefab:
//   - The MiniMap_Camera stays, but no longer renders (camera.enabled = false). It's kept straight down above
//     the player and turned with the player, because every blip script (MinimapBlipController,
//     PimlrMinimapEnemyBlips, FanMinimapBlip) places its icons with minimapCamera.WorldToViewportPoint - so they
//     all line up with the new map with no changes.
//   - The RawImage(s) showing MiniMapTex are hidden; a new RawImage with the baked map is put in their place,
//     behind the blips. Each frame it's moved so the player's map position is at the centre and rotated by the
//     player's heading (player-up).
// Self-starting in any scene that has a baked map (Resources/PIMLR/Minimap/<SceneName>); otherwise the old
// live minimap is left alone. Re-bake after level changes: PIMLR > Minimap > Bake 2D Map.
//
// Unreal note: a UMG minimap: an Image with the baked map texture in a circular retainer box, its render
// transform (translation + angle) driven from the pawn's location and yaw; icons placed by the same world -> map math.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class PimlrMinimap2D : MonoBehaviour
{
    const string LiveTextureName = "MiniMapTex";

    PimlrMinimapMap map;
    Camera mapCamera;
    Transform player;
    float cameraHeight = 70f;
    RawImage mapImage;
    float rescan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        var m = PimlrMinimapMap.ForScene(scene.name);
        if (!m || !m.texture) return;
        var go = new GameObject("[PIMLR_Minimap2D]");
        SceneManager.MoveGameObjectToScene(go, scene); // dies with the scene
        go.AddComponent<PimlrMinimap2D>().map = m;
    }

    void LateUpdate()
    {
        // The HUD (Global UI) and the player can appear a few frames after the scene loads.
        if (!mapCamera || !player || !mapImage)
        {
            rescan -= Time.unscaledDeltaTime;
            if (rescan > 0f) return;
            rescan = 0.5f;
            Resolve();
            if (!mapCamera || !player || !mapImage) return;
        }

        // Straight down over the player, turned with them (player-up). Not rendered - just the blips' projection.
        float yaw = player.eulerAngles.y;
        mapCamera.transform.SetPositionAndRotation(player.position + Vector3.up * cameraHeight, Quaternion.Euler(90f, yaw, 0f));

        // Map image: player's map position at the minimap centre, rotated by the heading.
        var parent = (RectTransform)mapImage.rectTransform.parent;
        float pxPerMetre = parent.rect.height / (2f * Mathf.Max(0.01f, mapCamera.orthographicSize));
        var rt = mapImage.rectTransform;
        rt.sizeDelta = map.worldSize * pxPerMetre;
        rt.pivot = new Vector2((player.position.x - map.worldMin.x) / map.worldSize.x,
                               (player.position.z - map.worldMin.y) / map.worldSize.y);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, yaw);

        // Player-up: the player's arrow always points up (MinimapBlipController turns it by the car's heading).
        if (!blips) blips = FindAnyObjectByType<MinimapBlipController>();
        if (blips && blips.playerBlip) blips.playerBlip.localRotation = Quaternion.identity;
    }

    MinimapBlipController blips;

    void Resolve()
    {
        if (!mapCamera)
        {
            foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                if (c.targetTexture && c.targetTexture.name == LiveTextureName) { mapCamera = c; break; }
            if (mapCamera)
            {
                cameraHeight = Mathf.Max(20f, mapCamera.transform.localPosition.y);
                mapCamera.enabled = false; // no more second render of the city
                var listener = mapCamera.GetComponent<AudioListener>();
                if (listener) Destroy(listener); // only the gameplay camera should hear
            }
        }
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!mapImage)
        {
            RawImage live = null;
            foreach (var ri in FindObjectsByType<RawImage>(FindObjectsInactive.Include))
            {
                if (!ri.texture || ri.texture.name != LiveTextureName) continue;
                // the one inside a Mask is the real minimap; any other copy is an old unused one
                if (!live || ri.GetComponentInParent<Mask>()) live = ri;
            }
            if (!live) return;
            foreach (var ri in FindObjectsByType<RawImage>(FindObjectsInactive.Include))
                if (ri.texture && ri.texture.name == LiveTextureName) ri.enabled = false;

            var go = new GameObject("MiniMap2D", typeof(RectTransform));
            go.transform.SetParent(live.transform.parent, false);
            go.transform.SetSiblingIndex(live.transform.GetSiblingIndex()); // behind the blips
            mapImage = go.AddComponent<RawImage>();
            mapImage.texture = map.texture;
            mapImage.raycastTarget = false;
            var rt = mapImage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        }
    }
}
