using JUTPS.FX;
using JUTPSEditor.JUHeader;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;


namespace JUTPS
{

    [AddComponentMenu("JU TPS/Third Person System/Additionals/JU Health")]
    public class JUHealth : MonoBehaviour
    {
        [JUHeader("Settings")]
        public float Health = 100;
        public float MaxHealth = 100;

        [JUHeader("Effects")]
        public bool BloodScreenEffect = false;
        public GameObject BloodHitParticle;

        [JUHeader("On Death Event")]
        public UnityEvent OnDeath;

        [JUHeader("Stats")]
        public bool IsDead;

        public Slider healthSlider;

        Rigidbody rb;
        Animator animator;

        public RCC_CarControllerV3 rcc_CarControllerV3;
        public RCC_AICarController rcc_AICarController;

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (gameObject.name == "My Player")
            {
                MaxHealth = 100;
            }
#endif
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            Health = MaxHealth;
        }

        void Start()
        {
            LimitHealth();
            InvokeRepeating(nameof(CheckHealthState), 0, 0.5f);
        }
        private void LimitHealth()
        {
            Health = Mathf.Clamp(Health, 0, MaxHealth);

            if (healthSlider != null)
            {
                healthSlider.maxValue = MaxHealth;
                healthSlider.value = Health;
            }

        }
        public static void DoDamage(JUHealth health, float damage, Vector3 hitPosition = default(Vector3))
        {
            health.DoDamage(damage, hitPosition);

        }
        public void DoDamage(float damage, Vector3 hitPosition = default(Vector3))
        {
            if (AuthManager.Instance)
            {
                if (AuthManager.Instance.achievementReward == AchievementReward.Defense && this.transform.name == "My Player")
                {
                    damage = (damage / 2);
                }

                if (AuthManager.Instance.achievementReward == AchievementReward.FreezeRay && this.transform.name != "My Player")
                {
                    if (gameObject.name != "Police (AI Chaser)")
                    {
                        rb.isKinematic = true;
                        animator.enabled = false;
                        Invoke(nameof(ReFreeze), 3);
                    }
                    else
                    {
                        if (rcc_CarControllerV3 != null)
                        {
                            Debug.Log("rcc_CarControllerV3rcc_CarControllerV3:");
                            rcc_CarControllerV3.maxspeed = 0;
                            rcc_CarControllerV3.externalController = false;
                            rcc_AICarController.Stop();
                            rcc_AICarController.enabled = false;
                        }
                        Invoke(nameof(ReFreeze), 6);
                    }

                }
            }
            Health -= damage;
            if (healthSlider != null)
                healthSlider.value = Health;

            LimitHealth();
            Invoke(nameof(CheckHealthState), 0.016f);

            if (BloodScreenEffect) BloodScreen.PlayerTakingDamaged();
            if (hitPosition != Vector3.zero && BloodHitParticle != null)
            {
                GameObject fxParticle = Instantiate(BloodHitParticle, hitPosition, Quaternion.identity);
                fxParticle.hideFlags = HideFlags.HideInHierarchy;
                Destroy(fxParticle, 3);
            }
        }




        void ReFreeze()
        {


            if (rcc_CarControllerV3 != null)
            {
                rcc_CarControllerV3.maxspeed = 200;
                rcc_CarControllerV3.externalController = true;
                rcc_AICarController.enabled = true;
                //rcc_AICarController.Stop();
            }
            else
            {
                rb.isKinematic = false;
                animator.enabled = true;
            }
        }
        public void CheckHealthState()
        {
            LimitHealth();

            if (Health <= 0 && IsDead == false)
            {
                Health = 0;
                IsDead = true;
                // OnSetPlayerCar();
                // GameExecutionManager.Instance.zombiesKilled = GameExecutionManager.Instance.zombiesKilled + 1;
                // PersistentAudioManager.Instance.musicPlayer.shopPointsInt += 5;


                if (healthSlider != null)
                    healthSlider.gameObject.SetActive(false);
                //Disable all damagers0
                foreach (Damager dmg in GetComponentsInChildren<Damager>()) dmg.gameObject.SetActive(false);
                OnDeath.Invoke();
                if (InfiniteMode.Instance)
                {
                    //InfiniteMode.Instance.OnKillEnemy();
                }
                else
                {
                    if (gameObject.name != "Police (AI Chaser)")
                        GameExecutionManager.Instance.OnKillEnemy();
                }
            }

            if (Health > 0) IsDead = false;
        }

        public void OnVideoPlay()
        {
            Debug.Log("   video play   " + this.gameObject.name);
            SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.EnemyCarDestroy);
            GameExecutionManager.Instance.playerInventory.gameObject.SetActive(false);
            SceneManagerScript.Instance.StartCoroutine(WaitForscreenfadeOut());
            if (AuthManager.Instance)
            {
                AuthManager.Instance.policeCarValue++;
            }
        }
        private IEnumerator WaitForscreenfadeOut()
        {
            //fadeOut
            UIFader fader = SceneManagerScript.Instance.uiManager.UI_fader;
            GameExecutionManager.Instance.AiCar.SetActive(false);
            GameExecutionManager.Instance.PlayerCar.SetActive(false);
            //GameExecutionManager.Instance.pla

            if (fader != null) fader.Fade(UIFader.FADE.FadeOut, 0.4f, 0.4f);
            yield return new WaitForSeconds(1f);
            SceneManagerScript.Instance.uiManager.ShowMenu("VideoScreen");
        }
        /*public void OnSetPlayerCar()
        {
            GameObject.FindAnyObjectByType<PlayerHandler>(). PlayerSpwanCar();
        }*/


        public void OnWashEnemy()
        {
            if (AuthManager.Instance)
            {
                AuthManager.Instance.enemyWashValue++;
            }
        }

        public void OnBossEnemyWash()
        {
            if (AuthManager.Instance)
            {
                AuthManager.Instance.bossWashValue++;
            }
        }

    }



}