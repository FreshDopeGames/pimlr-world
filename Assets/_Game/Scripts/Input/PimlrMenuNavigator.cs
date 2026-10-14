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

        // Right stick up/down scrolls the list on screen (leaderboards, music library...) - works whether or not
        // a button is focused, since leaderboard rows aren't buttons.
        if (menuUp) RightStickScroll(es);

        // Only a real menu (pause, music library, purchase popup, dialogs...) turns navigation on. The always-on
        // HUD buttons (music widget, coin/home bar, controls toggle) never do on their own - otherwise the cursor
        // would never re-lock in gameplay - but they ARE reachable while a menu is open.
        bool active = menuUp && items.Count > 0 && hasMenuButton;
        IsNavigating = active;
        SetModuleMove(es, !active);
        SetNavigationEvents(es, !active);

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
            if (index < 0) { var d = PickDefault(); es.SetSelectedGameObject(d.gameObject); ScrollIntoView(d); }
            index = IndexOf(es.currentSelectedGameObject);
            heldDir = ReadDirection(); // don't treat a still-held D-pad as a fresh press
        }
        wasActive = true;

        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame && TryCancel())
            return;

        // A / Cross: we own submit while navigating (the EventSystem's navigation events are off - see
        // SetNavigationEvents - so nothing else can move the focus behind our back).
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame && es.currentSelectedGameObject != null
            && CanSubmit(es, es.currentSelectedGameObject))
        {
            ExecuteEvents.Execute(es.currentSelectedGameObject, new BaseEventData(es), ExecuteEvents.submitHandler);
            rescanTimer = 0f;
        }

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

    // ---------- right-stick scrolling ----------

    const float StickDeadzone = 0.2f;
    const float ScrollPixelsPerSecond = 1400f;   // at full stick (canvas units)
    readonly List<ScrollRect> scrollCandidates = new List<ScrollRect>();
    float scrollRescanTimer;

    void RightStickScroll(EventSystem es)
    {
        var pad = Gamepad.current;
        if (pad == null) return;
        float y = pad.rightStick.ReadValue().y;
        if (Mathf.Abs(y) < StickDeadzone) return;

        scrollRescanTimer -= Time.unscaledDeltaTime;
        if (scrollRescanTimer <= 0f)
        {
            scrollCandidates.Clear();
            scrollCandidates.AddRange(FindObjectsOfType<ScrollRect>());
            scrollRescanTimer = 0.5f;
        }

        ScrollRect target = PickScrollTarget(es);
        if (target == null) return;

        var viewport = target.viewport ? target.viewport : (RectTransform)target.transform;
        float overflow = target.content.rect.height - viewport.rect.height;
        if (overflow <= 1f) return;

        // Ease in from the deadzone so small tilts scroll slowly and a full push scrolls fast.
        float amount = Mathf.Sign(y) * Mathf.InverseLerp(StickDeadzone, 1f, Mathf.Abs(y));
        amount *= amount * Mathf.Sign(amount);
        target.velocity = Vector2.zero;
        target.verticalNormalizedPosition = Mathf.Clamp01(
            target.verticalNormalizedPosition + amount * ScrollPixelsPerSecond * Time.unscaledDeltaTime / overflow);
    }

    /// <summary>The list the player is looking at: the one holding the focused button if any, otherwise the
    /// visible, scrollable list on the top-most canvas (largest one if there are several).</summary>
    ScrollRect PickScrollTarget(EventSystem es)
    {
        var selected = es != null ? es.currentSelectedGameObject : null;
        if (selected != null)
        {
            var own = selected.GetComponentInParent<ScrollRect>();
            if (Scrollable(own)) return own;
        }

        ScrollRect best = null;
        int bestOrder = int.MinValue;
        float bestArea = 0f;
        foreach (var sr in scrollCandidates)
        {
            if (!Scrollable(sr) || !OnScreen(sr)) continue;
            var canvas = sr.GetComponentInParent<Canvas>();
            int order = canvas ? canvas.rootCanvas.sortingOrder : 0;
            var vp = sr.viewport ? sr.viewport : (RectTransform)sr.transform;
            float area = vp.rect.width * vp.rect.height;
            if (order > bestOrder || (order == bestOrder && area > bestArea)) { best = sr; bestOrder = order; bestArea = area; }
        }
        return best;
    }

    static bool Scrollable(ScrollRect sr)
    {
        if (sr == null || !sr.isActiveAndEnabled || !sr.vertical || sr.content == null) return false;
        var vp = sr.viewport ? sr.viewport : (RectTransform)sr.transform;
        if (sr.content.rect.height - vp.rect.height <= 1f) return false;
        var group = sr.GetComponentInParent<CanvasGroup>();
        return group == null || group.alpha > 0.01f;
    }

    static readonly Vector3[] corners = new Vector3[4];
    static bool OnScreen(ScrollRect sr)
    {
        var vp = sr.viewport ? sr.viewport : (RectTransform)sr.transform;
        var canvas = sr.GetComponentInParent<Canvas>();
        Camera cam = canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        vp.GetWorldCorners(corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        Vector2 c = (a + b) * 0.5f;
        return c.x >= 0f && c.x <= Screen.width && c.y >= 0f && c.y <= Screen.height && (b - a).sqrMagnitude > 100f;
    }

    void OnDisable()
    {
        IsNavigating = false;
        SetModuleMove(EventSystem.current, true);
        SetNavigationEvents(EventSystem.current, true);
        HideRing();
    }

    // ---------- candidates ----------

    bool hasMenuButton;

    void Collect(EventSystem es)
    {
        items.Clear();
        hasMenuButton = false;
        if (pointer == null || pointerOwner != es) { pointer = new PointerEventData(es); pointerOwner = es; } // (eventSystem isn't public in UGUI 1.0)

        foreach (var s in Selectable.allSelectablesArray)
        {
            if (!(s is Button || s is Toggle || s is Dropdown || s is TMP_Dropdown || s is InputField || s is TMP_InputField))
                continue;
            if (!s.isActiveAndEnabled || !s.IsInteractable()) continue;
            if (s.TryGetComponent(out PimlrNavHint skipHint) && skipHint.skip) continue;

            var canvas = s.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) continue;
            var root = canvas.rootCanvas;
            // World-space canvases (Sirihanna's Dialogue_Canvas floats in the street) count too, as long as a camera
            // renders them; IsReachable below still requires the button to be on screen and not covered.
            if (root.renderMode == RenderMode.WorldSpace && CanvasCamera(root) == null) continue;

            if (!IsReachable(es, s, root)) continue;
            items.Add(s);
            if (!IsGameplayHud(s)) hasMenuButton = true;
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
    // Overlays that live inside HUD hierarchies but are real menus. Add a name here if a new popup is ever built
    // inside the JUTPS Interface or the music player.
    static readonly string[] OverlayRoots = { "Pause Screen", "Pause BG", "BlurrBG", "MusicLibrary", "Loot Items" };

    static bool IsGameplayHud(Selectable s)
    {
        if (s.TryGetComponent(out PimlrNavHint hint) && hint.isMenu) return false;
        for (Transform t = s.transform; t != null; t = t.parent)
        {
            if (System.Array.IndexOf(OverlayRoots, t.name) >= 0) return false;  // a menu inside the HUD
            if (t.name == "JUTPS Interface") return true;                       // gameplay HUD
            if (t.GetComponent<MusicSystem>() != null) return true;             // the music widget
        }
        return false;
    }

    // On screen and not covered by an overlay: a UI raycast at its centre must hit the button (or its children).
    bool IsReachable(EventSystem es, Selectable s, Canvas root)
    {
        // Rows in a scroll list (songs in the music library) may be scrolled out of view: they count if the list
        // itself is reachable, and get scrolled into view when selected (see ScrollIntoView).
        var scroll = s.GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.content != null && s.transform.IsChildOf(scroll.content)
            && !IsInsideViewport(s, scroll))
            return IsRectReachable(es, scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform, root, scroll.transform);

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

    // Last check before A presses a button: it must really be clickable right now (not under a "locked" cover,
    // not scrolled out of view).
    bool CanSubmit(EventSystem es, GameObject go)
    {
        var sel = go.GetComponent<Selectable>();
        if (sel == null) return true;
        if (!sel.IsInteractable()) return false;
        var canvas = sel.GetComponentInParent<Canvas>();
        if (canvas == null) return true;
        if (pointer == null || pointerOwner != es) { pointer = new PointerEventData(es); pointerOwner = es; }
        Vector2 p = ScreenCenter(sel);
        pointer.position = p;
        hits.Clear();
        es.RaycastAll(pointer, hits);
        foreach (var h in hits)
        {
            if (h.gameObject == null) continue;
            if (ring != null && h.gameObject.transform.IsChildOf(ring)) continue;
            return h.gameObject.transform.IsChildOf(sel.transform);
        }
        return false;
    }

    bool IsRectReachable(EventSystem es, RectTransform rt, Canvas root, Transform owner)
    {
        Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : CanvasCamera(root);
        Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        if (p.x < 0 || p.y < 0 || p.x > Screen.width || p.y > Screen.height) return false;
        pointer.position = p;
        hits.Clear();
        es.RaycastAll(pointer, hits);
        foreach (var h in hits)
        {
            if (h.gameObject == null) continue;
            if (ring != null && h.gameObject.transform.IsChildOf(ring)) continue;
            return h.gameObject.transform.IsChildOf(owner);
        }
        return false;
    }

    static bool IsInsideViewport(Selectable s, ScrollRect scroll)
    {
        var viewport = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
        Vector3 c = viewport.InverseTransformPoint(((RectTransform)s.transform).TransformPoint(((RectTransform)s.transform).rect.center));
        return viewport.rect.Contains(new Vector2(c.x, c.y));
    }

    // Keep the selected row of a scroll list visible.
    static void ScrollIntoView(Selectable s)
    {
        var scroll = s.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || !s.transform.IsChildOf(scroll.content)) return;
        var viewport = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
        var item = (RectTransform)s.transform;

        Canvas.ForceUpdateCanvases();
        Bounds vb = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item);
        Rect vr = viewport.rect;
        float dy = 0f;
        if (vb.max.y > vr.yMax) dy = vb.max.y - vr.yMax;          // above the window -> move content down
        else if (vb.min.y < vr.yMin) dy = vb.min.y - vr.yMin;     // below the window -> move content up
        if (Mathf.Abs(dy) > 0.01f)
        {
            scroll.content.anchoredPosition -= new Vector2(0f, dy);
            scroll.velocity = Vector2.zero;
        }
    }

    static Vector2 ScreenCenter(Selectable s)
    {
        var rt = (RectTransform)s.transform;
        var canvas = s.GetComponentInParent<Canvas>();
        var root = canvas != null ? canvas.rootCanvas : null;
        Camera cam = (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay) ? null : CanvasCamera(root);
        return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
    }

    static Camera CanvasCamera(Canvas root)
    {
        if (root.worldCamera != null && root.worldCamera.isActiveAndEnabled) return root.worldCamera;
        return root.renderMode == RenderMode.WorldSpace ? Camera.main : root.worldCamera;
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

        return best != null;
    }

    static readonly string[] DefaultNames = { "Story Mode", "Confirm", "Buy", "Purchase", "Yes", "OK" };

    Selectable PickDefault()
    {
        if (HasPreferredDefault(out var best)) return best;
        // A popup's confirm button first, otherwise the first menu button in reading order (never the HUD).
        foreach (var n in DefaultNames)
            foreach (var s in items)
                if (!IsGameplayHud(s) && s.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0) return s;
        foreach (var s in items)
            if (!IsGameplayHud(s)) return s;
        return items[0];
    }

    static readonly string[] CancelNames = { "Close", "Back", "Cancel" };
    // OnClick methods that close something (the music shop's close button is just called "Button", the pause
    // menu closes via its overlay's ExitInventory, Sirihanna via OnClosePanel...).
    static readonly string[] CancelMethods = { "OnCloseBtnClick", "OnClosePanel", "ExitInventory", "CloseMenu", "OnBackButton", "Close" };

    bool TryCancel()
    {
        Selectable target = FindCancelTarget(items);
        if (target == null)
        {
            // Not necessarily a focusable item (e.g. a full-screen overlay button behind the pause panel).
            var all = new List<Selectable>();
            foreach (var sel in Selectable.allSelectablesArray)
                if (sel is Button && sel.isActiveAndEnabled && sel.IsInteractable()) all.Add(sel);
            target = FindCancelTarget(all);
        }
        if (target == null) return false;

        if (target is Button b) b.onClick.Invoke();
        else ExecuteEvents.Execute(target.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        rescanTimer = 0f;
        return true;
    }

    // Best close/back button: explicit hint first, then menu (non-HUD) buttons by OnClick method or name.
    static Selectable FindCancelTarget(List<Selectable> pool)
    {
        foreach (var s in pool)
            if (s.TryGetComponent(out PimlrNavHint h) && h.isCancel) return s;

        for (int pass = 0; pass < 2; pass++)          // pass 0: menus only, pass 1: allow HUD
            foreach (var s in pool)
            {
                if (pass == 0 && IsGameplayHud(s)) continue;
                if (IsCancelButton(s)) return s;
            }
        return null;
    }

    static bool IsCancelButton(Selectable s)
    {
        foreach (var n in CancelNames)
            if (s.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (s is Button b)
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            {
                string m = b.onClick.GetPersistentMethodName(i);
                if (b.onClick.GetPersistentTarget(i) == null || string.IsNullOrEmpty(m)) continue;
                foreach (var cm in CancelMethods)
                    if (m == cm) return true;
            }
        return false;
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
            if (link != null && items.Contains(link)) { es.SetSelectedGameObject(link.gameObject); ScrollIntoView(link); return; }
        }

        var next = FindInDirection(from, dir);
        if (next != null) { es.SetSelectedGameObject(next.gameObject); ScrollIntoView(next); }
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

    // Any input module (or a second EventSystem's) would also move the focus with the D-pad / stick - that's
    // what kept dragging the main menu's focus from New Game back down to Continue. While we navigate, the
    // EventSystem sends no move/submit/cancel events at all; we do those ourselves.
    EventSystem navEventsOwner;
    void SetNavigationEvents(EventSystem es, bool restore)
    {
        if (restore)
        {
            if (navEventsOwner != null) navEventsOwner.sendNavigationEvents = true;
            navEventsOwner = null;
            return;
        }
        if (es == null || es == navEventsOwner) return;
        if (navEventsOwner != null) navEventsOwner.sendNavigationEvents = true;
        navEventsOwner = es;
        es.sendNavigationEvents = false;
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
            // Scale the ring to the button: small world-space buttons (Sirihanna's Next / x) get a thinner ring.
            var srt = (RectTransform)selected.transform;
            float h = Mathf.Max(1f, Mathf.Min(srt.rect.width, srt.rect.height));
            float pad = Mathf.Min(RingPadding, h * 0.25f);
            ringImage.pixelsPerUnitMultiplier = Mathf.Max(1f, 40f / h);
            ring.offsetMin = new Vector2(-pad, -pad);
            ring.offsetMax = new Vector2(pad, pad);
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
