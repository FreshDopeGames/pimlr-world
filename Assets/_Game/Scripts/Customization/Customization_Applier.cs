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
                // PIMLR: saved index, else this part's default (defaultColorIndices); out-of-range saves fall back to 0.
                int index = SavedOrDefaultIndex(i);
                // PIMLR: per-renderer material instance only. Writing to colourableMaterials changed the shared
                // Wolf3D_*.mat assets on disk every play session (git churn) and leaked colors between scenes.
                TintRenderer(renderers[i], colors[index]);
            }
        }
    }
}
