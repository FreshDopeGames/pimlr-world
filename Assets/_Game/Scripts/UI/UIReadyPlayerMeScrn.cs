using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UIReadyPlayerMeScrn : MonoBehaviour
{
    public GameObject WebGL;
    public GameObject Consol;
    // Start is called before the first frame update
    void Start()
    {

#if !UNITY_EDITOR && UNITY_WEBGL
        if (WebGL.activeSelf)
        {
            return;
        }
        WebGL.SetActive(true);
#endif
        if (Consol.activeSelf)
        {
            return;
        }
        Consol.SetActive(true);
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
