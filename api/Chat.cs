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
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
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
                var messages = GetChatMessages(incomingMessages);
                var options = GetCompletionOptions();
                var response = await chatClient.CompleteChatAsync(messages, options);
                if (response.Value.FinishReason == ChatFinishReason.ToolCalls)
                {
                    response = await ProcessToolCall(chatClient, messages, options, response);
                }
                string responseText = GetResponseText(response);
               
                if (responseText == PromptDefinition.StopConversationToken)
                {
                    return ActionResults.GetViolationResult();
                }
                return ActionResults.GetSuccessResult(responseText);

            }
            catch (System.ClientModel.ClientResultException ex)
            {
                if (ex.Status == 400 && ex.Message.Contains("content_filter"))// azure ai policy violation
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

        private static string GetResponseText(System.ClientModel.ClientResult<ChatCompletion> response)
        {
          return response?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;
        }

        private async Task<System.ClientModel.ClientResult<ChatCompletion>> ProcessToolCall(ChatClient chatClient, List<ChatMessage> messages, ChatCompletionOptions options, System.ClientModel.ClientResult<ChatCompletion> response)
        {
            messages.Add(new AssistantChatMessage(response.Value));
            foreach (var toolCall in response.Value.ToolCalls)
            {
                if (toolCall.FunctionName == Constants.GoogleSearchToolFunctionKey)
                {
                    var args = JsonSerializer.Deserialize<SearchGoogleArgs>(toolCall.FunctionArguments);
                    var searchService = new GoogleSearchService(configuration);
                    var toolResult = await searchService.SearchGoogle(args.query, args.maxResults, args.language);

                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                }
            }
            response = await chatClient.CompleteChatAsync(messages, options);
            return response;
        }

        private static List<ChatMessage> GetChatMessages(List<Message> incomingMessages)
        {
            List<ChatMessage> chatMessages =
                [
                     new SystemChatMessage(PromptDefinition.DefaultPrompt)
                ];

            foreach (var message in incomingMessages)
            {
                if (message.Sender == Constants.User)
                {
                    chatMessages.Add(new UserChatMessage(message.Text));
                }
                else if (message.Sender == Constants.Bot)
                {
                    chatMessages.Add(new AssistantChatMessage(message.Text));
                }
            }
            return chatMessages;
        }

        private ChatClient GetChatClient()
        {
            var azureOpenAiEndpoint = configuration[Constants.AzureOpenAiEndpointKey];
            var azureOpenAiApiKey = configuration[Constants.AzureOpenAiApiKey];
            var azureOpenAiDeploymentName = configuration[Constants.AzureOpenAiDeploymentNameKey];
            AzureOpenAIClient openAiClient = new(
                new Uri(azureOpenAiEndpoint), new AzureKeyCredential(azureOpenAiApiKey));
            ChatClient chatClient = openAiClient.GetChatClient(azureOpenAiDeploymentName);

            return chatClient;
        }

        private ChatCompletionOptions GetCompletionOptions()
        {
            return new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 4096,
                Temperature = 1.0f,
                TopP = 1.0f,
                Tools = { GoogleSearchService.GetSearchTool }
            };
        }
    }
}
