// #UI: repeatable lifecycle checks for the theme system (run from PIMLR > UI > Self Test, or step by step via MCP).
// Core invariant: PimlrUITheme.SubscriberCount == number of enabled Themed* components in loaded scenes.
// If that ever drifts, something subscribed without unsubscribing (a leak) or lost its subscription (stale UI).

using System.Collections.Generic;
using System.Linq;
using System.Text;
using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class PimlrUIThemeSelfTest
{
    public static int EnabledThemedCount() =>
        Object.FindObjectsByType<ThemedBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(b => b.isActiveAndEnabled);

    public static string Invariant()
    {
        int subs = PimlrUITheme.SubscriberCount, comps = EnabledThemedCount();
        return $"{(subs == comps ? "PASS" : "FAIL")} invariant: subscribers={subs}, enabled themed components={comps}";
    }

    [MenuItem("PIMLR/UI/Self Test (Edit Mode)")]
    public static string RunEditMode()
    {
        var sb = new StringBuilder();
        void R(bool ok, string what) => sb.AppendLine($"{(ok ? "PASS" : "FAIL")} {what}");

        sb.AppendLine(Invariant());
        int baseline = PimlrUITheme.SubscriberCount;

        // 1) Spawn / disable / re-enable / destroy 600 components: subscriptions must track exactly.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var canvas = new GameObject("SelfTest Canvas", typeof(RectTransform), typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas, scene);
        var spawned = new List<GameObject>();
        for (int i = 0; i < 200; i++)
        {
            var a = new GameObject("img", typeof(RectTransform), typeof(Image)); a.transform.SetParent(canvas.transform, false); a.AddComponent<ThemedImage>();
            var b = new GameObject("txt", typeof(RectTransform), typeof(TextMeshProUGUI)); b.transform.SetParent(canvas.transform, false); b.AddComponent<ThemedText>();
            var c = new GameObject("btn", typeof(RectTransform), typeof(Image), typeof(Button)); c.transform.SetParent(canvas.transform, false); c.AddComponent<ThemedButton>();
            spawned.Add(a); spawned.Add(b); spawned.Add(c);
        }
        R(PimlrUITheme.SubscriberCount == baseline + 600, $"spawn 600 -> subscribers {PimlrUITheme.SubscriberCount - baseline} added");
        for (int i = 0; i < 300; i++) spawned[i].SetActive(false);
        R(PimlrUITheme.SubscriberCount == baseline + 300, $"disable 300 -> {PimlrUITheme.SubscriberCount - baseline} remain");
        for (int i = 0; i < 300; i++) spawned[i].SetActive(true);
        foreach (var t in spawned.Take(50).Select(g => g.GetComponent<ThemedBehaviour>())) { t.enabled = false; t.enabled = true; t.enabled = true; }
        R(PimlrUITheme.SubscriberCount == baseline + 600, $"re-enable + toggle (no double subscribe) -> {PimlrUITheme.SubscriberCount - baseline}");
        foreach (var g in spawned) Object.DestroyImmediate(g);
        R(PimlrUITheme.SubscriberCount == baseline, $"destroy all -> back to baseline ({PimlrUITheme.SubscriberCount})");

        // 2) Raise with a destroyed-but-still-subscribed handler must not throw (defensive path).
        try { PimlrUITheme.RaiseChanged(); R(true, "RaiseChanged after mass destroy: no exception"); }
        catch (System.Exception e) { R(false, "RaiseChanged threw: " + e.Message); }

        // 3) Empty / incomplete theme: components must not throw, colors fall back to defaults, Validate reports errors.
        var empty = ScriptableObject.CreateInstance<PimlrUITheme>();
        var probe = new GameObject("probe", typeof(RectTransform), typeof(Image));
        probe.transform.SetParent(canvas.transform, false);
        var ti = probe.AddComponent<ThemedImage>(); ti.color = ColorToken.BrandLime; ti.themeOverride = empty;
        bool threw = false;
        try { ti.Apply(); } catch { threw = true; }
        var got = probe.GetComponent<Image>().color;
        R(!threw && Mathf.Abs(got.g - PimlrUITheme.DefaultColor(ColorToken.BrandLime).g) < 0.01f, "empty theme: no exception, lime falls back to default");
        var v = empty.Validate();
        R(v.Any(i => i.severity == PimlrUITheme.Severity.Error), $"empty theme validation reports {v.Count(i => i.severity == PimlrUITheme.Severity.Error)} error(s)");
        var dup = ScriptableObject.CreateInstance<PimlrUITheme>();
        dup.colors = new[] { new PimlrUITheme.ColorEntry { token = ColorToken.BrandLime }, new PimlrUITheme.ColorEntry { token = ColorToken.BrandLime } };
        R(dup.Validate().Any(i => i.message.Contains("BrandLime appears 2 times")), "duplicate token detected");
        var real = PimlrUITheme.Current;
        var realIssues = real ? real.Validate() : null;
        R(real && realIssues.All(i => i.severity != PimlrUITheme.Severity.Error),
            $"project theme: {(realIssues == null ? "missing" : string.Join("; ", realIssues.Where(i => i.severity != PimlrUITheme.Severity.Info).Select(i => i.ToString())) + " (no errors expected)")}");
        Object.DestroyImmediate(empty); Object.DestroyImmediate(dup);

        // 4) ThemedButton must preserve everything except its own styling.
        var host = new GameObject("listener host");
        host.transform.SetParent(canvas.transform, false);
        var bgo = new GameObject("btn under test", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        bgo.transform.SetParent(canvas.transform, false);
        var other = new GameObject("other", typeof(RectTransform), typeof(Image), typeof(Button)); other.transform.SetParent(canvas.transform, false);
        var btn = bgo.GetComponent<Button>();
        btn.transition = Selectable.Transition.SpriteSwap;
        btn.interactable = false;
        btn.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = other.GetComponent<Button>(), wrapAround = false };
        UnityEventTools.AddBoolPersistentListener(btn.onClick, new UnityAction<bool>(host.SetActive), false);
        var before = Snapshot(btn);
        var tb = ObjectFactory.AddComponent<ThemedButton>(bgo); // Inspector-style add: runs Reset()
        tb.Apply(); real?.Refresh();
        var after = Snapshot(btn);
        R(tb.transitionMode == ThemedButton.TransitionMode.KeepExisting, "Sprite Swap button auto-set to Keep Existing");
        R(before == after, before == after ? "onClick listeners, navigation, interactable, transition, components unchanged" : $"changed:\n  before {before}\n  after  {after}");
        tb.transitionMode = ThemedButton.TransitionMode.ThemeColorTint; btn.transition = Selectable.Transition.ColorTint; tb.Apply();
        var tinted = Snapshot(btn);
        R(tinted.Replace("ColorTint", "SpriteSwap") == before, "Theme Color Tint mode: only the transition/colors changed, listeners + navigation + interactable kept");

        EditorSceneManager.CloseScene(scene, true);
        sb.AppendLine(Invariant());
        return sb.ToString();
    }

    static string Snapshot(Button b)
    {
        var n = b.navigation;
        var listeners = string.Join(",", Enumerable.Range(0, b.onClick.GetPersistentEventCount())
            .Select(i => $"{(b.onClick.GetPersistentTarget(i) ? b.onClick.GetPersistentTarget(i).name : "null")}.{b.onClick.GetPersistentMethodName(i)}"));
        var comps = string.Join(",", b.GetComponents<Component>().Select(c => c.GetType().Name).Where(x => x != nameof(ThemedButton)));
        return $"listeners=[{listeners}] nav={n.mode}/{(n.selectOnDown ? n.selectOnDown.name : "-")} interactable={b.interactable} transition={b.transition} comps=[{comps}]";
    }
}
