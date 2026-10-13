using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// PIMLR: controller menu navigation for every scene (added to the PIMLR_Input object by PimlrInputRuntime).
/// Active when a controller is the current input and a menu is up (cursor visible):
///   - auto-selects a button so there is always a focus,
///   - D-pad / left stick cycles through every visible, clickable button in reading order
///     (top-to-bottom, left-to-right; buttons covered by an overlay are skipped),
///   - A / Cross confirms (the scene's InputSystemUIInputModule submits the selected button),
///   - a pulsing green ring is drawn around the selected button.
/// While active, JUInputManager mutes gameplay buttons so A doesn't also jump, etc.
/// </summary>
public class PimlrMenuNavigator : MonoBehaviour
{
    public static bool IsNavigating { get; private set; }

    const float FirstRepeatDelay = 0.35f;
    const float RepeatInterval = 0.12f;
    const float RescanInterval = 0.2f;
    const float RingPadding = 8f;
    static readonly Color Accent = new Color(0.095f, 0.745f, 0f, 1f); // SAVE-button green

    readonly List<Selectable> items = new List<Selectable>();
    readonly List<RaycastResult> hits = new List<RaycastResult>();
    PointerEventData pointer;
    EventSystem pointerOwner;

    RectTransform ring;
    Image ringImage;
    Sprite ringSprite;

    float rescanTimer, repeatTimer;
    int heldDir;

    InputSystemUIInputModule mutedModule;
    InputActionReference mutedMove;

    void Update()
    {
        var es = EventSystem.current;
        bool menuUp = es != null && PimlrInput.UsingGamepad && Cursor.visible;

        if (menuUp)
        {
            rescanTimer -= Time.unscaledDeltaTime;
            if (rescanTimer <= 0f || IndexOf(es.currentSelectedGameObject) < 0)
            {
                Collect(es);
                rescanTimer = RescanInterval;
            }
        }

        bool active = menuUp && items.Count > 0;
        IsNavigating = active;
        SetModuleMove(es, !active);

        if (!active)
        {
            HideRing();
            heldDir = 0;
            return;
        }

        int index = IndexOf(es.currentSelectedGameObject);
        if (index < 0)
        {
            index = 0;
            es.SetSelectedGameObject(items[0].gameObject);
        }

        int dir = ReadDirection();
        if (dir != 0)
        {
            if (dir != heldDir)
            {
                Step(es, index, dir);
                repeatTimer = FirstRepeatDelay;
            }
            else
            {
                repeatTimer -= Time.unscaledDeltaTime;
                if (repeatTimer <= 0f)
                {
                    Step(es, index, dir);
                    repeatTimer = RepeatInterval;
                }
            }
        }
        heldDir = dir;

        ShowRing(es.currentSelectedGameObject);
    }

    void OnDisable()
    {
        IsNavigating = false;
        SetModuleMove(EventSystem.current, true);
        HideRing();
    }

    // ---------- candidates ----------

    void Collect(EventSystem es)
    {
        items.Clear();
        if (pointer == null || pointerOwner != es) { pointer = new PointerEventData(es); pointerOwner = es; } // (eventSystem isn't public in UGUI 1.0)

        foreach (var s in Selectable.allSelectablesArray)
        {
            if (!(s is Button || s is Toggle || s is Dropdown || s is TMP_Dropdown || s is InputField || s is TMP_InputField))
                continue;
            if (!s.isActiveAndEnabled || !s.IsInteractable()) continue;
            if (IsGameplayHud(s)) continue;

            var canvas = s.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) continue;
            var root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.WorldSpace) continue;

            if (!IsReachable(es, s, root)) continue;
            items.Add(s);
        }

        // Reading order: rows top-to-bottom (24px tolerance), then left-to-right.
        items.Sort((a, b) =>
        {
            Vector2 pa = ScreenCenter(a), pb = ScreenCenter(b);
            if (Mathf.Abs(pa.y - pb.y) > 24f) return pb.y.CompareTo(pa.y);
            return pa.x.CompareTo(pb.x);
        });
    }

    // The always-on gameplay HUD isn't a menu: the JUTPS Interface (Controls Info toggle etc.) everywhere, and the
    // music widget in YannicksWorld (music is on Y / LB / RB there). Otherwise its buttons would keep the
    // navigator active during gameplay and swallow A / D-pad.
    static bool IsGameplayHud(Selectable s)
    {
        for (Transform t = s.transform; t != null; t = t.parent)
            if (t.name == "JUTPS Interface") return true;

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "YannicksWorld"
            && s.GetComponentInParent<MusicSystem>(true) != null)
            return true;

        return false;
    }

    // On screen and not covered by an overlay: a UI raycast at its centre must hit the button (or its children).
    bool IsReachable(EventSystem es, Selectable s, Canvas root)
    {
        Vector2 p = ScreenCenter(s);
        if (p.x < 0 || p.y < 0 || p.x > Screen.width || p.y > Screen.height) return false;

        pointer.position = p;
        hits.Clear();
        es.RaycastAll(pointer, hits);
        foreach (var h in hits)
        {
            if (h.gameObject == null) continue;
            if (ring != null && h.gameObject.transform.IsChildOf(ring)) continue;
            return h.gameObject.transform.IsChildOf(s.transform);
        }
        return false;
    }

    static Vector2 ScreenCenter(Selectable s)
    {
        var rt = (RectTransform)s.transform;
        var root = s.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera cam = (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay) ? null : root.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
    }

    int IndexOf(GameObject go)
    {
        if (go == null) return -1;
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && items[i].gameObject == go) return i;
        return -1;
    }

    // ---------- input ----------

    static int ReadDirection()
    {
        var pad = Gamepad.current;
        if (pad == null) return 0;

        Vector2 v = pad.dpad.ReadValue();
        if (v.sqrMagnitude < 0.25f)
        {
            v = pad.leftStick.ReadValue();
            if (v.magnitude < 0.6f) return 0;
        }

        // Down / Right = next, Up / Left = previous.
        if (Mathf.Abs(v.y) >= Mathf.Abs(v.x)) return v.y < -0.5f ? 1 : (v.y > 0.5f ? -1 : 0);
        return v.x > 0.5f ? 1 : (v.x < -0.5f ? -1 : 0);
    }

    void Step(EventSystem es, int index, int dir)
    {
        int next = (index + dir + items.Count) % items.Count;
        es.SetSelectedGameObject(items[next].gameObject);
    }

    // The UI module's own spatial navigation would also react to the D-pad; mute it while we navigate.
    void SetModuleMove(EventSystem es, bool restore)
    {
        if (restore)
        {
            if (mutedModule != null) mutedModule.move = mutedMove;
            mutedModule = null;
            mutedMove = null;
            return;
        }

        var module = es != null ? es.currentInputModule as InputSystemUIInputModule : null;
        if (module == null || module == mutedModule) return;
        if (mutedModule != null) mutedModule.move = mutedMove;
        mutedModule = module;
        mutedMove = module.move;
        module.move = null;
    }

    // ---------- highlight ring ----------

    void ShowRing(GameObject selected)
    {
        if (selected == null) { HideRing(); return; }
        if (ring == null) CreateRing();

        if (ring.parent != selected.transform)
        {
            ring.SetParent(selected.transform, false);
            ring.SetAsFirstSibling(); // behind the button's label/icons
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.one;
            ring.pivot = new Vector2(0.5f, 0.5f);
            ring.offsetMin = new Vector2(-RingPadding, -RingPadding);
            ring.offsetMax = new Vector2(RingPadding, RingPadding);
            ring.localScale = Vector3.one;
            ring.localRotation = Quaternion.identity;
        }

        if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
        ringImage.color = new Color(Accent.r, Accent.g, Accent.b, pulse);
    }

    void HideRing()
    {
        if (ring != null && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
    }

    void CreateRing()
    {
        if (ringSprite == null) ringSprite = BuildRingSprite();

        var go = new GameObject("PIMLR_SelectHighlight", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        ring = (RectTransform)go.transform;
        ringImage = go.AddComponent<Image>();
        ringImage.sprite = ringSprite;
        ringImage.type = Image.Type.Sliced;
        ringImage.raycastTarget = false;
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        go.SetActive(false);
    }

    // 32x32 rounded-rect outline (4px stroke, 10px corner radius), 9-sliced so it fits any button size.
    static Sprite BuildRingSprite()
    {
        const int size = 32;
        const float radius = 10f, stroke = 4f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        Vector2 half = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
            float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
            float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + stroke + 0.5f);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
    }
}
