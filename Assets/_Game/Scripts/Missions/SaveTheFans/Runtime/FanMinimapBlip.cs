// PIMLR #STF (Save The Fans) — lights the fan up on the minimap.
// Built the same way as PimlrMinimapEnemyBlips so it lines up with the red enemy blips: same
// minimap camera, same circle-masked blip layer (the parent of the player blip), same rule for
// off-map targets — pinned to the rim in the fan's direction from the player.
//
// The minimap camera's orthographic size is 15, so the map only shows about 15 m around the
// player. A fan further away than that is ALWAYS on the rim; that rim blip is the player's
// "go this way" compass during the travel timer. An optional arrow child points outward.
//
// Unreal note: a UMG minimap widget with a list of tracked actors; same viewport math against
// the minimap SceneCapture2D (or plain world -> map UV scaling).

using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.Missions.SaveTheFans
{
    [AddComponentMenu("PIMLR/Save The Fans/Fan Minimap Blip")]
    public class FanMinimapBlip : MonoBehaviour
    {
        [Header("Minimap (leave empty: taken from MinimapBlipController)")]
        public Camera minimapCamera;
        public RectTransform blipLayer;

        [Header("Blip")]
        [Tooltip("UI prefab for the blip (an Image, ideally a gold star). If empty a gold diamond is generated.")]
        public RectTransform blipPrefab;
        [Tooltip("Optional child of the blip prefab, pointing UP in its default pose. Shown only while the fan is off-map.")]
        public string edgeArrowChildName = "EdgeArrow";
        public Color blipColor = new Color(1f, 0.76f, 0.18f, 1f);
        [Tooltip("Enemy blips are 9, boss blips 14. The objective should be the biggest thing on the map.")]
        [Min(4f)] public float blipSize = 16f;
        [Min(0f)] public float rimPadding = 3f;
        [Tooltip("The PIMLR minimap is round. Untick only if you swap in a square one.")]
        public bool circularMinimap = true;

        [Header("Pulse")]
        [Min(0f)] public float pulseSpeed = 1.6f;
        [Range(0f, 0.6f)] public float pulseScale = 0.25f;

        [Tooltip("Optional explicit target; defaults to this transform.")]
        public Transform target;

        RectTransform blip;
        RectTransform edgeArrow;
        Transform player;
        bool visible;

        void Awake()
        {
            if (!target) target = transform;
        }

        bool EnsureBlip()
        {
            if (blip) return true;

            if (!minimapCamera || !blipLayer)
            {
                MinimapBlipController controller = SceneManagerScript.Instance ? SceneManagerScript.Instance.minimapBlipController : null;
                if (!controller) controller = FindAnyObjectByType<MinimapBlipController>(FindObjectsInactive.Include);
                if (controller)
                {
                    if (!minimapCamera) minimapCamera = controller.minimapCamera;
                    if (!blipLayer && controller.playerBlip) blipLayer = controller.playerBlip.parent as RectTransform;
                    if (!blipLayer) blipLayer = controller.minimapUI;
                }
            }
            if (!minimapCamera || !blipLayer) return false; // HUD not built yet; try again next frame

            if (blipPrefab)
            {
                blip = Instantiate(blipPrefab, blipLayer);
            }
            else
            {
                var go = new GameObject("PIMLR_FanBlip_" + name, typeof(RectTransform), typeof(Image));
                blip = (RectTransform)go.transform;
                blip.SetParent(blipLayer, false);
                blip.sizeDelta = new Vector2(blipSize, blipSize);
                blip.localRotation = Quaternion.Euler(0f, 0f, 45f); // diamond: reads differently from the round enemy dots
            }

            blip.gameObject.layer = blipLayer.gameObject.layer;
            blip.anchorMin = blip.anchorMax = new Vector2(0.5f, 0.5f);
            blip.pivot = new Vector2(0.5f, 0.5f);
            foreach (var img in blip.GetComponentsInChildren<Image>(true))
            {
                img.color = blipColor;
                img.raycastTarget = false;
            }
            if (!blip.GetComponent<LayoutElement>()) blip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            blip.SetAsLastSibling(); // above enemy blips: it's the objective

            var arrowT = string.IsNullOrEmpty(edgeArrowChildName) ? null : blip.Find(edgeArrowChildName);
            edgeArrow = arrowT as RectTransform;
            blip.gameObject.SetActive(visible);
            return true;
        }

        public void SetVisible(bool on)
        {
            visible = on;
            if (EnsureBlip()) blip.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (!visible || !EnsureBlip() || !target) return;
            if (!blip.gameObject.activeSelf) blip.gameObject.SetActive(true);

            if (!player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }

            Rect rect = blipLayer.rect;
            Vector3 vp = minimapCamera.WorldToViewportPoint(target.position);
            Vector2 local = new Vector2((vp.x - 0.5f) * rect.width, (vp.y - 0.5f) * rect.height);

            bool outside;
            if (circularMinimap)
            {
                float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
                float limit = radius - blipSize * 0.5f - rimPadding;
                outside = local.magnitude > limit;
                if (outside)
                {
                    // Direction from the player's blip, pinned to the rim (same as the enemy blips).
                    Vector3 playerVp = player ? minimapCamera.WorldToViewportPoint(player.position) : new Vector3(0.5f, 0.5f, 0f);
                    Vector2 fromPlayer = local - new Vector2((playerVp.x - 0.5f) * rect.width, (playerVp.y - 0.5f) * rect.height);
                    Vector2 dir = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : local.normalized;
                    local = dir * limit;
                }
            }
            else
            {
                float hx = rect.width * 0.5f - rimPadding, hy = rect.height * 0.5f - rimPadding;
                Vector2 c = new Vector2(Mathf.Clamp(local.x, -hx, hx), Mathf.Clamp(local.y, -hy, hy));
                outside = c != local;
                local = c;
            }

            blip.anchoredPosition = local;
            float s = 1f + pulseScale * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f));
            blip.localScale = new Vector3(s, s, 1f);

            if (edgeArrow)
            {
                if (edgeArrow.gameObject.activeSelf != outside) edgeArrow.gameObject.SetActive(outside);
                if (outside)
                {
                    float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg - 90f;
                    edgeArrow.rotation = blipLayer.rotation * Quaternion.Euler(0f, 0f, angle);
                }
            }
        }

        void OnDestroy()
        {
            if (blip) Destroy(blip.gameObject);
        }
    }
}
