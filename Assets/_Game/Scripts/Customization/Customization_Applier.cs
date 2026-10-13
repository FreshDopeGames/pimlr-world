using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Customization_Applier : changeColor
{


    #region UnityCallbacks
    private void Start()
    {
        GetPlayPrefs();
        ApplyCustomization();
    }
    #endregion


    void GetPlayPrefs()
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    string rendererName = renderers[i].name;
                    PlayerPrefs.GetInt(rendererName, 0);
                }
            }
        }
    }


    void ApplyCustomization()
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                string rendererName = renderers[i].name;
                // PIMLR: guard against saved indices outside the palette (e.g. from an older/newer build).
                int index = PlayerPrefs.GetInt(rendererName, 0);
                if (index < 0 || index >= colors.Length) index = 0;
                renderers[i].materials[0].color = colors[index];
                if (colourableMaterials != null && i < colourableMaterials.Length && colourableMaterials[i] != null)
                    colourableMaterials[i].color = colors[index];
            }
        }
    }
}
