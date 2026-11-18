using UnityEngine;

public class changeColor : MonoBehaviour
{
    #region Private Variables
    private int currentCarIndex = 0;
    int selectedRenderedInt;
    #endregion

    #region
    public Color[] colors;//= { Color.red, Color.blue, Color.green, Color.yellow };
    public GameObject[] cars; // Store references to the different car GameObjects
    public Renderer[] renderers;
    public Material[] colourableMaterials; // Materials for each car
    public Material selectedMaterial;

    // PIMLR: palette index each part shows before the player has saved a pick (parallel to `renderers`:
    // 0 Shirt, 1 Hair, 2 Pants, 3 Shoes). Part textures are authored white-based, so the tint IS the
    // default look; leaving an entry missing falls back to 0 (White).
    [Tooltip("Default PimlrPalette index per renderer (same order as Renderers). Used until the player saves a color.")]
    public int[] defaultColorIndices = { 9, 3, 6, 9 };
    #endregion

    /// <summary>Saved palette index for renderer slot i, or that slot's default if nothing is saved yet.</summary>
    /// <summary>PIMLR: tint a renderer's first material and its distance-LOD twin ("&lt;name&gt;_LOD1" under the same parent).</summary>
    public static void TintRenderer(Renderer r, Color c)
    {
        if (r == null) return;
        r.materials[0].color = c;
        var parent = r.transform.parent;
        if (parent == null) return;
        var lod = parent.Find(r.name + "_LOD1");
        if (lod != null && lod.TryGetComponent<Renderer>(out var lr)) lr.materials[0].color = c;
        // PIMLR (Thurz v4): the hair colour also drives the head renderer. Its tint mask covers only hair-coloured
        // texels (hairline, beard, brows), so skin is untouched and the hairline follows the painted texture
        // instead of the mesh split between Renderer_Hair and Renderer_Head.
        if (r.name.EndsWith("Hair"))
        {
            var head = parent.Find(r.name.Substring(0, r.name.Length - 4) + "Head");
            if (head != null && head != r.transform) TintRenderer(head.GetComponent<Renderer>(), c);
        }
    }

    protected int SavedOrDefaultIndex(int i)
    {
        if (renderers == null || i < 0 || i >= renderers.Length || renderers[i] == null) return -1;
        int fallback = (defaultColorIndices != null && i < defaultColorIndices.Length) ? defaultColorIndices[i] : 0;
        int index = PlayerPrefs.GetInt(renderers[i].name, fallback);
        return (colors != null && index >= 0 && index < colors.Length) ? index : 0;
    }

    // PIMLR: every scene uses the shared 12-color palette, so saved indices mean the same color everywhere
    // (the per-scene serialized `colors` arrays had drifted and only held 9 entries).
    protected virtual void Awake()
    {
        colors = (Color[])PimlrPalette.Colors.Clone();
    }

    void Start()
    {
        UpdateActiveCar();
        ApplySavedColors();
    }

    // PIMLR: re-apply each part's saved color (PlayerPrefs, keyed by renderer name) when the scene loads.
    // Without this, SceneStaticEU showed the shared white materials until the player picked a color again.
    // Uses per-renderer material instances, so the shared material assets are left untouched.
    public void ApplySavedColors()
    {
        if (renderers == null || colors == null || colors.Length == 0) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            TintRenderer(renderers[i], colors[SavedOrDefaultIndex(i)]);
        }
    }

    /// <summary>Saved palette index for a part (0 Shirt, 1 Hair, 2 Pants, 3 Shoes), or -1 if unknown.</summary>
    public int GetSavedColorIndex(int clothIndex)
    {
        return SavedOrDefaultIndex(clothIndex);
    }
    private void UpdateActiveCar()
    {
        if (cars == null) return;
        for (int i = 0; i < cars.Length; i++)
        {
            cars[i].SetActive(i == currentCarIndex);
        }
    }

    public void NextCar()
    {
        currentCarIndex = (currentCarIndex + 1) % cars.Length;
        UpdateActiveCar();
    }

    public void PreviousCar()
    {
        currentCarIndex--;
        if (currentCarIndex < 0)
            currentCarIndex = cars.Length - 1;
        UpdateActiveCar();
    }

    public void SelectCloth(int i)
    {
        selectedRenderedInt = i; 
    }


    public void SetColor(int i)
    {
        //selectedMaterial.color = colors[i];
        // changes done 
        //renderer[selectedRenderedInt].materials[0].SetColor("Base Color", colors[i]);

        if (colors == null || i < 0 || i >= colors.Length) return;
        if (renderers == null || selectedRenderedInt < 0 || selectedRenderedInt >= renderers.Length || renderers[selectedRenderedInt] == null) return;

        TintRenderer(renderers[selectedRenderedInt], colors[i]);
        string rendererName = renderers[selectedRenderedInt].name;
        PlayerPrefs.SetInt(rendererName, i);
        PlayerPrefs.Save();
    }



    #region Buttons
    public void OnRed()
    {
        SetColor(1);
    }

    public void OnBlue()
    {
        SetColor(0);
    }

    public void OnGreen()
    {
        SetColor(2);
    }

    public void OnBlack()
    {
        SetColor(3);
    }
    public void OnBrown()
    {
        SetColor(4);
    }
    public void OnOff_White()
    {
        SetColor(5);
    }
    public void OnNavy_Blue()
    {
        SetColor(6);
    }
    public void OnOrange()
    {
        SetColor(7);
    }
    
    public void OnLeopard()
    {
        SetColor(8);
    }

    #endregion



}


