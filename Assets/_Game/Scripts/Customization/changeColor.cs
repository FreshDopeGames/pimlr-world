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

    void Start()
    {
        UpdateActiveCar();
    }
    private void UpdateActiveCar()
    {
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

        Debug.Log(":::::>>>>" + renderers.Length + "|||||||||||" + selectedRenderedInt);
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


