using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// PIMLR: controller menu navigation for every scene (added to the PIMLR_Input object by PimlrInputRuntime).
/// Active when a controller is the current input and a menu is up (cursor visible):
///   - auto-selects a button when a menu opens (a PimlrNavHint default, e.g. Story Mode / Sirihanna's Next,
///     otherwise the first button in reading order),
///   - D-pad / left stick moves spatially: up/down/left/right go to the nearest button in that direction.
///     Moving up/down into a different group (another panel, the music player) lands on that group's FIRST
///     button; inside a group (a grid of swatches, a column of tabs) it keeps the same column/row,
///   - A / Cross confirms (the scene's InputSystemUIInputModule submits the selected button),
///   - B / Circle presses the visible Close / Back / Cancel button (or a PimlrNavHint marked as cancel),
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
    Vector2Int heldDir;
    bool wasActive;

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
            heldDir = Vector2Int.zero;
            wasActive = false;
            return;
        }

        int index = IndexOf(es.currentSelectedGameObject);
        if (index < 0 || !wasActive)
        {
            // A menu just opened (or the focused button went away): go to its default button.
            if (index < 0 || HasPreferredDefault(out _)) es.SetSelectedGameObject(PickDefault().gameObject);
            index = IndexOf(es.currentSelectedGameObject);
            heldDir = ReadDirection(); // don't treat a still-held D-pad as a fresh press
        }
        wasActive = true;

        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame && TryCancel())
            return;

        Vector2Int dir = ReadDirection();
        if (dir != Vector2Int.zero)
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
            if (s.TryGetComponent(out PimlrNavHint skipHint) && skipHint.skip) continue;

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

    static Vector2Int ReadDirection()
    {
        var pad = Gamepad.current;
        if (pad == null) return Vector2Int.zero;

        Vector2 v = pad.dpad.ReadValue();
        if (v.sqrMagnitude < 0.25f)
        {
            v = pad.leftStick.ReadValue();
            if (v.magnitude < 0.6f) return Vector2Int.zero;
        }

        if (Mathf.Abs(v.y) >= Mathf.Abs(v.x)) return v.y > 0.5f ? Vector2Int.up : (v.y < -0.5f ? Vector2Int.down : Vector2Int.zero);
        return v.x > 0.5f ? Vector2Int.right : (v.x < -0.5f ? Vector2Int.left : Vector2Int.zero);
    }

    // ---------- defaults / cancel ----------

    bool HasPreferredDefault(out Selectable best)
    {
        best = null; int bestPriority = int.MinValue;
        foreach (var s in items)
            if (s.TryGetComponent(out PimlrNavHint h) && h.defaultPriority > 0 && h.defaultPriority > bestPriority)
            { best = s; bestPriority = h.defaultPriority; }
        if (best == null)
            foreach (var s in items)
                if (s.name.IndexOf("Story Mode", System.StringComparison.OrdinalIgnoreCase) >= 0) { best = s; break; }
        return best != null;
    }

    Selectable PickDefault() => HasPreferredDefault(out var best) ? best : items[0];

    static readonly string[] CancelNames = { "Close", "Back", "Cancel" };

    bool TryCancel()
    {
        Selectable target = null;
        foreach (var s in items)
            if (s.TryGetComponent(out PimlrNavHint h) && h.isCancel) { target = s; break; }
        if (target == null)
            foreach (var s in items)
            {
                foreach (var n in CancelNames)
                    if (s.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0) { target = s; break; }
                if (target != null) break;
            }
        if (target == null) return false;

        if (target is Button b) b.onClick.Invoke();
        else ExecuteEvents.Execute(target.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        rescanTimer = 0f;
        return true;
    }

    // ---------- spatial movement ----------

    void Step(EventSystem es, int index, Vector2Int dir)
    {
        if (index < 0 || index >= items.Count) return;
        var from = items[index];

        // Explicit links win.
        if (from.TryGetComponent(out PimlrNavHint hint))
        {
            var link = dir == Vector2Int.up ? hint.up : dir == Vector2Int.down ? hint.down : dir == Vector2Int.left ? hint.left : hint.right;
            if (link != null && items.Contains(link)) { es.SetSelectedGameObject(link.gameObject); return; }
        }

        var next = FindInDirection(from, dir);
        if (next != null) es.SetSelectedGameObject(next.gameObject);
    }

    Selectable FindInDirection(Selectable from, Vector2Int dir)
    {
        Vector2 a = ScreenCenter(from);
        Vector2 d = dir;
        bool vertical = dir.y != 0;
        const float RowTolerance = 24f;

        // 1) everything ahead of us in that direction (within a 60 degree-ish cone, generous for grids)
        var ahead = new List<Selectable>();
        foreach (var s in items)
        {
            if (s == from) continue;
            Vector2 v = ScreenCenter(s) - a;
            float along = Vector2.Dot(v, d);
            if (along <= 4f) continue;
            float across = Mathf.Abs(vertical ? v.x : v.y);
            if (!vertical && across > Mathf.Max(along * 1.2f, RowTolerance)) continue; // left/right: stay roughly in the row
            ahead.Add(s);
        }
        if (ahead.Count == 0) return null;

        if (!vertical)
        {
            // Nearest, strongly preferring the same row.
            return ahead.OrderBy(s => { Vector2 v = ScreenCenter(s) - a; return Mathf.Abs(v.x) + 3f * Mathf.Abs(v.y); }).First();
        }

        // 2) vertical: the nearest row in that direction ...
        float nearest = ahead.Min(s => Mathf.Abs(ScreenCenter(s).y - a.y));
        var row = ahead.Where(s => Mathf.Abs(ScreenCenter(s).y - a.y) - nearest <= RowTolerance).ToList();

        // ... same group (same parent: a grid / a column of tabs) -> keep the column;
        // different group (another panel, the music player) -> its first (left-most) button.
        var sameGroup = row.Where(s => s.transform.parent == from.transform.parent).ToList();
        if (sameGroup.Count > 0)
            return sameGroup.OrderBy(s => Mathf.Abs(ScreenCenter(s).x - a.x)).First();

        // A closer row in our own group further away would be odd UI; prefer it only if nothing else is in the row.
        return row.OrderBy(s => ScreenCenter(s).x).First();
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
