using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Customization_Handler : MonoBehaviour
{
    #region private variables
    private int selectedRenderedInt;
    private Customization_Applier customizationApplier;
    #endregion

    #region public Variables
    [SerializeField]
    GameObject[] colorsPanel;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        customizationApplier = FindObjectOfType<Customization_Applier>();
        
    }

    private void OnDisable()
    {
        if (customizationApplier != null)
        {
            for (int i = 0; i < customizationApplier.colourableMaterials.Length; i++)
            {
                customizationApplier.colourableMaterials[i].color = Color.white;
            }
        }
    }
    #endregion



    public void PanelChanger(string catalogueName)
    {
        CloseAllPanels();

        if (catalogueName.Equals("shirtColorCatalogue"))
        {
            colorsPanel[0].SetActive(true);
            SelectCloth(0);
        }
        else if (catalogueName.Equals("hairColorCatalogue"))
        {
            colorsPanel[1].SetActive(true);
            SelectCloth(1);

        }
        else if (catalogueName.Equals("pantsColorCatalogue"))
        {
            colorsPanel[2].SetActive(true);
            SelectCloth(2);

        }
        else if (catalogueName.Equals("shoesColorCatalogue"))
        {
            colorsPanel[3].SetActive(true);
            SelectCloth(3);
        }
        else
        {
            Debug.Log("------   none catelogue    -----");
        }
    }

    public void SelectCloth(int i)
    {
        //for render texture model
        customizationApplier.selectedMaterial = customizationApplier.colourableMaterials[i];

        //for player
        selectedRenderedInt = i;
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

    void SetColor(int i)
    {
        //for render texture model
        customizationApplier.selectedMaterial.color = customizationApplier. colors[i];

        //changes done//for player
        customizationApplier.renderers[selectedRenderedInt].materials[0].color = customizationApplier.colors[i];
        string rendererName = customizationApplier.renderers[selectedRenderedInt].name;
        PlayerPrefs.SetInt(rendererName, i);
        PlayerPrefs.Save();

    }
    void CloseAllPanels()
    {
        for (int i = 0; i < colorsPanel.Length; i++)
        {
            colorsPanel[i].SetActive(false);
        }
    }
}


