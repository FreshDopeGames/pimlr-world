using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PIMLR: rewrites the JUTPS "Controls Info" panel for whichever controls are active (keyboard & mouse,
/// Xbox or PlayStation) and refreshes when the player switches. Installed automatically by PimlrInputRuntime on
/// any "Controls Info" object that has the JUTPS row layout; the original rows are hidden, not deleted, and one
/// of them ("Move Buttons") is used as the visual template.
/// </summary>
public class PimlrControlsInfo : MonoBehaviour
{
    struct Row
    {
        public string Key, Action;
        public Row(string key, string action) { Key = key; Action = action; }
    }

    static readonly Row[] KeyboardRows =
    {
        new Row("WASD", "Move"),
        new Row("Mouse", "Camera"),
        new Row("Shift", "Run"),
        new Row("Space", "Jump"),
        new Row("C", "Crouch"),
        new Row("Z", "Prone"),
        new Row("Ctrl", "Roll"),
        new Row("L Mouse", "Fire"),
        new Row("R Mouse", "Aim"),
        new Row("R", "Reload"),
        new Row("F", "Interact / Ride"),
        new Row("Q / E", "Switch Weapon"),
        new Row("Tab", "Inventory"),
    };

    // {Xbox, PlayStation, action}
    static readonly string[,] GamepadRows =
    {
        { "L Stick", "L Stick", "Move" },
        { "R Stick", "R Stick", "Camera" },
        { "RT", "R2", "Fire  |  Drive: Accelerate" },
        { "LT", "L2", "Aim  |  Drive: Brake / Reverse" },
        { "A", "Cross", "Jump / Interact" },
        { "X", "Square", "Reload" },
        { "B", "Circle", "Crouch / Prone / Stand  |  Exit Vehicle" },
        { "Y", "Triangle", "Play / Pause Track" },
        { "LB", "L1", "Previous Track" },
        { "RB", "R1", "Next Track" },
        { "L3", "L3", "Run" },
        { "D-Pad L/R", "D-Pad L/R", "Switch Weapon" },
        { "Start", "Options", "Menu / Pause" },
    };

    RectTransform template;
    Text header;
    float startY, spacing, boxLeft, descGap;
    readonly System.Collections.Generic.List<RectTransform> rows = new System.Collections.Generic.List<RectTransform>();
    bool built;

    public static void InstallAll()
    {
        foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rt.name != "Controls Info" || rt.Find("Move Buttons") == null) continue;
            if (!rt.GetComponent<PimlrControlsInfo>()) rt.gameObject.AddComponent<PimlrControlsInfo>();
        }
    }

    void Awake() => Build();

    void OnEnable()
    {
        PimlrInput.SchemeChanged += Refresh;
        Refresh();
    }

    void OnDisable() => PimlrInput.SchemeChanged -= Refresh;

    void Build()
    {
        if (built) return;
        template = transform.Find("Move Buttons") as RectTransform;
        var first = transform.Find("Camera Buttons") as RectTransform;
        if (!template) return;

        var headerT = transform.Find("Keyboard Controls Text");
        header = headerT ? headerT.GetComponent<Text>() : null;

        startY = first ? first.anchoredPosition.y : template.anchoredPosition.y;
        spacing = first ? Mathf.Abs(first.anchoredPosition.y - template.anchoredPosition.y) : 46f;
        if (spacing < 30f) spacing = 46f;

        var box = template.Find("Keyboard Control") as RectTransform;
        var desc = box ? box.Find("Text (1)") as RectTransform : null;
        boxLeft = box ? box.anchoredPosition.x - box.sizeDelta.x * 0.5f : -20f;
        descGap = (box && desc) ? desc.anchoredPosition.x - box.sizeDelta.x * 0.5f : 128f;

        // Hide the original hard-coded keyboard rows.
        foreach (Transform child in transform)
            if (child != headerT && child.Find("Keyboard Control") != null) child.gameObject.SetActive(false);

        built = true;
    }

    void Refresh()
    {
        if (!built) Build();
        if (!built) return;

        var scheme = PimlrInput.Scheme;
        int count = scheme == PimlrControlScheme.KeyboardMouse ? KeyboardRows.Length : GamepadRows.GetLength(0);

        if (header)
            header.text = scheme == PimlrControlScheme.KeyboardMouse ? "KEYBOARD & MOUSE"
                        : scheme == PimlrControlScheme.PlayStation ? "PLAYSTATION CONTROLLER" : "XBOX CONTROLLER";

        while (rows.Count < count)
        {
            var row = (RectTransform)Instantiate(template, transform);
            row.name = "PIMLR_Row_" + rows.Count;
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            bool on = i < count;
            row.gameObject.SetActive(on);
            if (!on) continue;

            string key, action;
            if (scheme == PimlrControlScheme.KeyboardMouse) { key = KeyboardRows[i].Key; action = KeyboardRows[i].Action; }
            else { key = GamepadRows[i, scheme == PimlrControlScheme.PlayStation ? 1 : 0]; action = GamepadRows[i, 2]; }

            row.anchoredPosition = new Vector2(row.anchoredPosition.x, startY - i * spacing);
            SetRow(row, key, action);
        }
    }

    void SetRow(RectTransform row, string key, string action)
    {
        var box = row.Find("Keyboard Control") as RectTransform;
        if (!box) return;

        float width = Mathf.Clamp(20f + key.Length * 11f, 40f, 130f);
        box.sizeDelta = new Vector2(width, box.sizeDelta.y);
        box.anchoredPosition = new Vector2(boxLeft + width * 0.5f, box.anchoredPosition.y);

        var keyText = box.Find("Text") ? box.Find("Text").GetComponent<Text>() : null;
        if (keyText) keyText.text = key;

        var descT = box.Find("Text (1)") as RectTransform;
        if (descT)
        {
            descT.anchoredPosition = new Vector2(width * 0.5f + descGap, descT.anchoredPosition.y);
            var d = descT.GetComponent<Text>();
            if (d) d.text = action;
        }
    }
}
