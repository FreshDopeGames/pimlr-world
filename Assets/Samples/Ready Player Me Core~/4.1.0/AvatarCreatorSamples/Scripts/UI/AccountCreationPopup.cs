using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReadyPlayerMe
{
    public class AccountCreationPopup : MonoBehaviour
    {
        [SerializeField] private InputField emailField;
        [SerializeField] private Button sendEmailButton;
        [SerializeField] private Button continueWithoutSignupButton;
        [SerializeField] private Button closeButton;

        public event Action<string> OnSendEmail;
        public event Action OnContinueWithoutSignup;


        //Abhay
        //public GameObject _screeen;
        //public GameObject _SceneChangesButton;

        private void Update()
        {
            var email = emailField.text;
            sendEmailButton.interactable = !string.IsNullOrEmpty(email) && ValidatorUtil.IsValidEmail(email);
        }

        private void OnEnable()
        {
            sendEmailButton.onClick.AddListener(OnSendEmailButton);
            continueWithoutSignupButton.onClick.AddListener(OnContinueWithoutSignupButton);
            closeButton.onClick.AddListener(OnContinueWithoutSignupButton);
        }

        private void OnDisable()
        {
            sendEmailButton.onClick.RemoveListener(OnSendEmailButton);
            continueWithoutSignupButton.onClick.RemoveListener(OnContinueWithoutSignupButton);
            closeButton.onClick.RemoveListener(OnContinueWithoutSignupButton);
        }


        private void OnSendEmailButton()
        {
            var email = emailField.text;
            OnSendEmail?.Invoke(email);
            gameObject.SetActive(false);
        }

        private void OnContinueWithoutSignupButton()
        {
            OnContinueWithoutSignup?.Invoke();
            gameObject.SetActive(false);
            //SceneManager.LoadScene("03_LevelSelection");
        }
    }
}
