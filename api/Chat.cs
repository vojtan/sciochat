using OpenAI.Chat;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Scio.ChatBotApi
{
    public class Message
    {
        public string Text { get; set; } = "";
        public string Sender { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class Chat
    {
        private readonly IConfiguration configuration;

        public Chat(ILogger<Chat> logger, IConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [Function("chat")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", "get")] HttpRequest req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var incomingMessages = JsonSerializer.Deserialize<List<Message>>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (incomingMessages == null || incomingMessages.Count == 0)
                {
                    return ActionResults.GetErrorResult("No messages provided");
                }

                var chatClient = GetChatClient();
                var response = chatClient.CompleteChat(GetChatMessages(incomingMessages), GetCompletionOptions());
                if (response.Value.Content[0].Text == AiConfiguration.StopConversationToken)
                {
                    return ActionResults.GetViolationResult();
                }
                return ActionResults.GetSuccessResult(response.Value.Content[0].Text);

            }
            catch (System.ClientModel.ClientResultException ex)
            {
                if (ex.Status == 400)// azure ai policy violation
                {
                    return ActionResults.GetViolationResult(ex.Message);
                }
                throw;
            }
            catch (Exception ex)
            {
                return ActionResults.GetErrorResult(ex.Message);
            }
        }

        private static List<ChatMessage> GetChatMessages(List<Message> incomingMessages)
        {
            List<ChatMessage> chatMessages =
                [
                     new SystemChatMessage(AiConfiguration.DefaultPrompt)
                ];

            foreach (var message in incomingMessages)
            {
                if (message.Sender == "user")
                {
                    chatMessages.Add(new UserChatMessage(message.Text));
                }
                else if (message.Sender == "bot")
                {
                    chatMessages.Add(new AssistantChatMessage(message.Text));
                }
            }

            return chatMessages;
        }

        private ChatClient GetChatClient()
        {
            var azureOpenAiEndpoint = configuration["AzureOpenAiEndpoint"];
            var azureOpenAiApiKey = configuration["AzureOpenAiApiKey"];
            var azureOpenAiDeploymentname = configuration["AzureOpenAiDeploymentName"];
            AzureOpenAIClient openAiClient = new(
                new Uri(azureOpenAiEndpoint), new AzureKeyCredential(azureOpenAiApiKey));
            ChatClient chatClient = openAiClient.GetChatClient(azureOpenAiDeploymentname);
            return chatClient;
        }

        private static ChatCompletionOptions GetCompletionOptions()
        {
            return new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 4096,
                Temperature = 1.0f,
                TopP = 1.0f,
            };
        }
    }
}
