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
    #endregion

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
        foreach (var r in renderers)
        {
            if (r == null) continue;
            int index = PlayerPrefs.GetInt(r.name, 0);
            if (index < 0 || index >= colors.Length) index = 0;
            r.materials[0].color = colors[index];
        }
    }

    /// <summary>Saved palette index for a part (0 Shirt, 1 Hair, 2 Pants, 3 Shoes), or -1 if unknown.</summary>
    public int GetSavedColorIndex(int clothIndex)
    {
        if (renderers == null || clothIndex < 0 || clothIndex >= renderers.Length || renderers[clothIndex] == null) return -1;
        int index = PlayerPrefs.GetInt(renderers[clothIndex].name, 0);
        return (colors != null && index >= 0 && index < colors.Length) ? index : 0;
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

        renderers[selectedRenderedInt].materials[0].color = colors[i];
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


