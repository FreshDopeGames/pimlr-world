using System;
using System.Collections.Generic;
using ReadyPlayerMe.AvatarCreator;
using ReadyPlayerMe.Core;
using ReadyPlayerMe.Core.Analytics;
using UnityEngine;
using UnityEngine.UI;

namespace ReadyPlayerMe
{
    public class AvatarCreatorStateMachine : StateMachine
    {
        private const string TAG = nameof(AvatarCreatorStateMachine);
        
        [SerializeField] private List<State> states;
        [SerializeField] private Button backButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private LoadingManager loadingManager;
        [SerializeField] private StateType startingState;
        [SerializeField] public AvatarCreatorData avatarCreatorData;
        [SerializeField] private ProfileManager profileManager;

        public Action<string> AvatarSaved;


       /* public GameObject _ReadyPlayerMeUI;
        public GameObject _SceneChangesButton;
        public GameObject avatar;*/

        private void Start()
        {
            AnalyticsRuntimeLogger.EventLogger.LogAvatarCreatorSample(CoreSettingsHandler.CoreSettings.AppId);
            
            if (string.IsNullOrEmpty(CoreSettingsHandler.CoreSettings.AppId))
            {
                Debug.LogError("App ID is missing. Please put your App-ID in Ready Player Me > Settings.");
                return;
            }

            avatarCreatorData.AvatarProperties.Partner = CoreSettingsHandler.CoreSettings.Subdomain;
            Initialize();
            
            SetState(profileManager.LoadSession() ? StateType.AvatarSelection : startingState);
        }

        private void OnEnable()
        {
            StateChanged += OnStateChanged;
            AuthManager.OnSignedIn += OnSignedIn;
            AuthManager.OnSignedOut += OnSignedOut;
            AuthManager.OnSessionRefreshed += OnSessionRefreshed;
            backButton.onClick.AddListener(GoToPreviousState);
        }

        private void OnDisable()
        {
            StateChanged -= OnStateChanged;
            AuthManager.OnSignedIn -= OnSignedIn;
            AuthManager.OnSignedOut -= OnSignedOut;
            AuthManager.OnSessionRefreshed -= OnSessionRefreshed;
            backButton.onClick.RemoveListener(GoToPreviousState);
        }
        
        private void OnSignedIn(UserSession userSession)
        {
            profileManager.SaveSession(userSession);
        }

        private void OnSignedOut()
        {
            avatarCreatorData.AvatarProperties.Id = string.Empty;
            SetState(startingState);
            ClearPreviousStates();
        }
        
        private void OnSessionRefreshed(UserSession userSession)
        {
            profileManager.SaveSession(userSession);
            SDKLogger.Log(TAG, $"Session refreshed for userId: {userSession.Id}");
        }

        private void Initialize()
        {
            foreach (var state in states)
            {
                state.Initialize(this, avatarCreatorData, loadingManager);
            }
            base.Initialize(states);
            //Debug.Log("Done");
        }

        private void OnStateChanged(StateType current, StateType previous)
        {
            backButton.gameObject.SetActive(!CanShowBackButton(current));
            saveButton.gameObject.SetActive(current == StateType.Editor);

            if (current == StateType.End)
            {
               // _ReadyPlayerMeUI.SetActive(true);
               // _SceneChangesButton.SetActive(true);
                AvatarSaved?.Invoke(avatarCreatorData.AvatarProperties.Id);
                Debug.Log("dataa:"+  avatarCreatorData.AvatarProperties.Id);
                Debug.Log($"https://models.readyplayer.me/{avatarCreatorData.AvatarProperties.Id}.glb");
                //avatar = 
            }
        }

        private bool CanShowBackButton(StateType current)
        {
            return current == StateType.BodyTypeSelection || current == StateType.LoginWithCodeFromEmail || current == StateType.AvatarSelection;
        }
    }
}
