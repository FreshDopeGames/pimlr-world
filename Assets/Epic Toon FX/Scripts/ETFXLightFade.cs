using UnityEngine;
using System.Collections;

namespace EpicToonFX
{
    public class ETFXLightFade : MonoBehaviour
    {
        [Header("Seconds to dim the light")]
        public float life = 0.2f;
        public bool killAfterLife = true;

        private Light li;
        private float initIntensity;

        // Use this for initialization
        void Start()
        {
            if (gameObject.GetComponent<Light>())
            {
                li = gameObject.GetComponent<Light>();
                initIntensity = li.intensity;
            }
            else
                print("No light object found on " + gameObject.name);
        }

        // PIMLR: the original removed the Light component when the fade finished. Under URP every Light carries a
        // UniversalAdditionalLightData that depends on it, so the removal failed and logged
        // "Can't remove Light because UniversalAdditionalLightData (Script) depends on it" EVERY FRAME for the rest of
        // the effect's life (hundreds of errors per enemy-portal wave, which made the Editor stutter). Now the light is
        // just switched off and this script stops running. It also no longer calls GetComponent twice a frame.
        void Update()
        {
            if (li == null) { enabled = false; return; }

            li.intensity -= initIntensity * (Time.deltaTime / life);
            if (li.intensity <= 0f)
            {
                li.intensity = 0f;
                if (killAfterLife) li.enabled = false;
                enabled = false;
            }
        }
    }
}