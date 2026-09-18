using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

    public class ResponseHandler : MonoBehaviour
    {
        [SerializeField] private RectTransform responseBox;
        [SerializeField] private RectTransform responseButtonTemplate;
        [SerializeField] private Button nextButton;
        [SerializeField] internal RectTransform responseContainer;
        [SerializeField] private RectTransform mixamalsAnswersToggle;

        private Dialogue dialogue;

        private List<GameObject> tempResponseButtons = new List<GameObject>();
        private List<Response> tempResponses = new List<Response>();

        [SerializeField] private StringEvent buttonClicked;
        private void Start()
        {
            dialogue = GetComponent<Dialogue>();
            HideNextButton();
        }

        public void ShowNextButton()
        {
            if (nextButton == null)
                return;

            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(dialogue.Next);
            nextButton.gameObject.SetActive(true);
        }

        public void HideNextButton()
        {
            if (nextButton != null)
                nextButton.gameObject.SetActive(false);
        }


        public void ShowResponses(Response[] responses, ActiveGame activeState)
        {
            float responseBoxWidth = 0;
            for (int i = 0; i < responses.Length; i++)
            {
                Response response = responses[i];
                int responseIndex = i;
                GameObject responseButton = Instantiate(responseButtonTemplate.gameObject, responseContainer);

                responseButton.GetComponentInChildren<TMP_Text>().text = response.ResponseText;
                if (activeState == ActiveGame.WalkingAround)
                {
                    responseButton.GetComponent<Button>().onClick.AddListener(() => OnClickResponse(response, responseIndex));
                }
                responseButton.gameObject.SetActive(true);

                tempResponseButtons.Add(responseButton);

                responseBoxWidth += responseButtonTemplate.sizeDelta.x;
            }


            StartCoroutine("OptionshoenDelay");
            responseBox.sizeDelta = new Vector2(responseBoxWidth, responseBox.sizeDelta.y);
            responseBox.gameObject.SetActive(true);
        }
        IEnumerator OptionshoenDelay()
        {
            yield return new WaitForSeconds(0.09f);
            responseContainer.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.09f);
            responseContainer.gameObject.SetActive(true);
        }

        public void ShowResponsesToogle(Response[] responses, ActiveGame activeState)
        {
            float responseBoxWidth = 0;
            for (int i = 0; i < responses.Length; i++)
            {
                Response response = responses[i];
                int responseIndex = i;
                GameObject responseButton = Instantiate(mixamalsAnswersToggle.gameObject, responseContainer);
                responseButton.gameObject.SetActive(true);
                responseButton.GetComponentInChildren<TMP_Text>().text = response.ResponseText;
                if (activeState == ActiveGame.Mixamals)
                {
                    responseButton.GetComponent<Toggle>().onValueChanged.AddListener(delegate { OnClickMixamals(response.ResponseText); });
                }

                tempResponses.Add(response);
                tempResponseButtons.Add(responseButton);

                responseBoxWidth += responseButtonTemplate.sizeDelta.x;
            }

            responseBox.sizeDelta = new Vector2(responseBoxWidth, responseBox.sizeDelta.y);
            responseBox.gameObject.SetActive(true);
        }

        public void ResetTogglesMixamals()
        {
            for (int i = 0; i < tempResponseButtons.Count; i++)
            {
                //        tempResponseButtons[i].GetComponent<Toggle>().onValueChanged.RemoveAllListeners();
                tempResponseButtons[i].GetComponent<Toggle>().SetIsOnWithoutNotify(false);
                //         tempResponseButtons[i].GetComponent<Toggle>().onValueChanged.AddListener(delegate { OnClickMixamals(tempResponses[i].ResponseText); });
            }
        }


        private void OnClickResponse(Response response, int responseIndex)
        {
            //SoundController.Instance.UIclick();

            responseBox.gameObject.SetActive(false);

            foreach (GameObject button in tempResponseButtons)
            {
                Destroy(button);
            }
            tempResponseButtons.Clear();
            tempResponses.Clear();
            if (response.DialogueScriptable != null)
            {
                dialogue.ShowDialogue(response.DialogueScriptable);
            }

            if (response.ResponseEventVoid != null)
                response.ResponseEventVoid.Raise();
            if (response.ResponseEventDialogue != null)
                response.ResponseEventDialogue.Raise(response.DialogueScriptableEventPayload);
            if (response.ResponseEventVector3 != null)
                response.ResponseEventVector3.Raise(response.Vector3EventPayload);

            //   dialogue.ShowDialogue(response.DialogueScriptable);

        }

        private void OnClickMixamals(string responseText)
        {
            buttonClicked.Raise(responseText);
        }

        public void ClearPreviousAnswer()
        {
            if (responseBox != null)
            {
                responseBox.gameObject.SetActive(false);
            }

            foreach (GameObject button in tempResponseButtons)
            {

                Destroy(button);
            }
            tempResponseButtons.Clear();

        }


        /// <summary>
        /// Made by S.A. for Wrong Answer after disable Options
        /// </summary>
        public void Disablebutton()
        {

            foreach (GameObject button in tempResponseButtons)
            {

                button.GetComponent<Toggle>().interactable = false;
            }
        }


        /// <summary>
        /// Made by S.A. for Wrong Answer after Retry Click On enable Options
        /// </summary>
        public void EnableButton()
        {
            foreach (GameObject button in tempResponseButtons)
            {

                button.GetComponent<Toggle>().interactable = true;
            }
        }
    }


public enum ActiveGame
{
    WalkingAround,
    Mixamals
}