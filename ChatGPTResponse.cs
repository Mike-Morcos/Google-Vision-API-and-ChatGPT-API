
using FrostweepGames.Plugins.GoogleCloud.Vision;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;


namespace OpenAI
{
    public class ChatGPTResponse : MonoBehaviour
    {
        public static int visionTokenCount;


        //[SerializeField] private Button submitButton;
        [SerializeField] private TextMeshProUGUI responseText;

        private OpenAIApi openai;
        //private string userInput;
        private List<ChatMessage> messages = new List<ChatMessage>();
        private string conversationInstruction = "Solve the problem. with the steps on how you completed it ";
        //private string annotationText;
        public static string annotatedResponseText;
        [SerializeField] GameObject waitingIcon;


        private void Start()
        {

            responseText.text = "";

            openai = new OpenAIApi("Your API key");
           
        }



        public IEnumerator SubmitChat()
        {
            waitingIcon.SetActive(true);
            responseText.text = "Thinking";
            int currentToken = PlayfabManager.instance.GetCurrentToken();
            if (currentToken <= 0)
            {
                waitingIcon.SetActive(true);
                responseText.text = "Not enough Tokens please visit the store on the main menu.";
                yield break; // Exit the coroutine if the current token is 0 or less
            }
            yield return new WaitForSeconds(1f);
            

            var newMessage = new ChatMessage()
            {
                Role = "user",
                Content = conversationInstruction.TrimEnd('\n') + " " + annotatedResponseText
            };

            messages.Add(newMessage);

            // Call the async method inside a coroutine
            var completionTask = openai.CreateChatCompletion(new CreateChatCompletionRequest()
            {
                Model = "gpt-3.5-turbo-0301",
                Messages = messages
            });
            while (!completionTask.IsCompleted) yield return null;
            var completionResponse = completionTask.Result;

            if (completionResponse.Choices != null && completionResponse.Choices.Count > 0)
            {
                var message = completionResponse.Choices[0].Message;
                message.Content = message.Content.Trim();

                messages.Add(message); 

                waitingIcon.SetActive(false);

                responseText.text = message.Content;
                conversationInstruction += $"{annotatedResponseText}\nQ: ";

                //----------------------------------------------------------------------------
                PlayfabManager.instance.SubtractToken();
                //----------------------------------------------------------------------------
            }
            else
            {
                responseText.text = "Servers are down.";
            }
            annotatedResponseText = "";

        }
        public void CopyResponseText()
        {
            GUIUtility.systemCopyBuffer = responseText.text;
        }
    }
}