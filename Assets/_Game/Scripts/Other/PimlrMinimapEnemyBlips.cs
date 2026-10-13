using System.Collections.Generic;
using JUTPS;
using JUTPS.CharacterBrain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PIMLR: red blips for every living enemy on the round minimap. Enemies inside the minimap's view sit where
/// they are; enemies beyond it are pinned to the rim in their direction from the player (slightly smaller and
/// faded) so you always know which way the next fight is.
/// Auto-installs next to MinimapBlipController in YannicksWorld; uses the same minimap camera and blip layer.
/// </summary>
public class PimlrMinimapEnemyBlips : MonoBehaviour
{
    public Color enemyColor = new Color(1f, 0.16f, 0.1f, 1f);
    public Color bossColor = new Color(1f, 0.55f, 0f, 1f);
    public float blipSize = 9f;
    public float bossBlipSize = 14f;
    [Range(0.3f, 1f)] public float edgeScale = 0.8f;
    [Range(0f, 1f)] public float edgeAlpha = 0.75f;
    public float rimPadding = 3f;
    public float rescanInterval = 0.5f;

    Camera mapCamera;
    RectTransform layer;            // circle-masked blip parent
    readonly List<JUCharacterBrain> enemies = new List<JUCharacterBrain>();
    readonly List<Image> blips = new List<Image>();
    Transform player;
    float rescanTimer;
    static Sprite dot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "YannicksWorld") return;
        foreach (var c in FindObjectsByType<MinimapBlipController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!c.GetComponent<PimlrMinimapEnemyBlips>()) c.gameObject.AddComponent<PimlrMinimapEnemyBlips>();
    }

    void Start()
    {
        var controller = GetComponent<MinimapBlipController>();
        if (controller != null)
        {
            mapCamera = controller.minimapCamera;
            if (controller.playerBlip) layer = controller.playerBlip.parent as RectTransform;
            if (!layer) layer = controller.minimapUI;
        }
        if (!mapCamera)
        {
            var go = GameObject.Find("MiniMap_Camera");
            if (go) mapCamera = go.GetComponent<Camera>();
        }
        if (!mapCamera || !layer)
        {
            Debug.LogWarning("PIMLR Minimap: couldn't find the minimap camera or blip layer - enemy blips disabled.", this);
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (!mapCamera || !layer) return;
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        rescanTimer -= Time.unscaledDeltaTime;
        if (rescanTimer <= 0f) { Rescan(); rescanTimer = rescanInterval; }

        Rect r = layer.rect;
        float radius = Mathf.Min(r.width, r.height) * 0.5f;
        Vector3 playerVp = player ? mapCamera.WorldToViewportPoint(player.position) : new Vector3(0.5f, 0.5f, 0f);

        int used = 0;
        foreach (var e in enemies)
        {
            if (!e || e.IsDead || !e.gameObject.activeInHierarchy) continue;

            bool boss = IsBoss(e);
            float size = boss ? bossBlipSize : blipSize;

            // Viewport -> blip-layer local. The camera follows (and turns with) the player, so this keeps the
            // same heading-up orientation as the map image.
            Vector3 vp = mapCamera.WorldToViewportPoint(e.transform.position);
            Vector2 local = new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height);

            float limit = radius - size * 0.5f - rimPadding;
            bool outside = local.magnitude > limit;
            if (outside)
            {
                // Direction from the player's blip, pinned to the rim.
                Vector2 fromPlayer = local - new Vector2((playerVp.x - 0.5f) * r.width, (playerVp.y - 0.5f) * r.height);
                Vector2 dir = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : local.normalized;
                local = dir * (radius - size * edgeScale * 0.5f - rimPadding);
            }

            var img = Blip(used++);
            img.rectTransform.anchoredPosition = local; // anchored to the layer's centre
            float s = outside ? size * edgeScale : size;
            img.rectTransform.sizeDelta = new Vector2(s, s);
            Color c = boss ? bossColor : enemyColor;
            c.a = outside ? edgeAlpha : 1f;
            img.color = c;
        }
        for (int i = used; i < blips.Count; i++)
            if (blips[i].gameObject.activeSelf) blips[i].gameObject.SetActive(false);
    }

    void Rescan()
    {
        enemies.Clear();
        foreach (var b in FindObjectsByType<JUCharacterBrain>(FindObjectsSortMode.None))
            if (b.IsArtificialIntelligence && !b.IsDead && !b.CompareTag("Player")) enemies.Add(b);
    }

    static bool IsBoss(JUCharacterBrain e)
    {
        if (e.name.IndexOf("Boss", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            e.name.IndexOf("WaterBallon", System.StringComparison.OrdinalIgnoreCase) < 0) return true;
        var h = e.GetComponent<JUHealth>();
        return h != null && h.MaxHealth >= 500f;
    }

    Image Blip(int i)
    {
        while (blips.Count <= i)
        {
            var go = new GameObject("PIMLR_EnemyBlip", typeof(RectTransform));
            go.layer = layer.gameObject.layer;
            go.transform.SetParent(layer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.sprite = Dot();
            img.raycastTarget = false;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            // Above the map image (first child), below the player / car markers.
            go.transform.SetSiblingIndex(Mathf.Min(1, layer.childCount - 1));
            blips.Add(img);
        }
        var b = blips[i];
        if (!b.gameObject.activeSelf) b.gameObject.SetActive(true);
        return b;
    }

    // Soft-edged white disc with a dark outline (tinted by Image.color).
    static Sprite Dot()
    {
        if (dot) return dot;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, rad = n * 0.5f - 1f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
            float a = Mathf.Clamp01(rad - d + 0.5f);
            float shade = d > rad - 4f ? 0.25f : 1f; // outline ring
            byte v = (byte)(255 * shade);
            px[y * n + x] = new Color32(v, v, v, (byte)(255 * a));
        }
        tex.SetPixels32(px);
        tex.Apply();
        dot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return dot;
    }
}
