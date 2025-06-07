using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace Scio.ChatBotApi
{
    public class SearchGoogleArgs
    {
        public string query { get; set; }
        public int maxResults { get; set; } = 5;
        public string language { get; set; } = "en";
    }

    public class GoogleSearchResponse
    {
        public GoogleSearchItem[]? Items { get; set; }
    }

    public class GoogleSearchItem
    {
        public string Title { get; set; } = "";
        public string Link { get; set; } = "";
        public string Snippet { get; set; } = "";
    }

    public class GoogleSearchService(IConfiguration configuration)
    {
        public static ChatTool GetSearchTool = ChatTool.CreateFunctionTool(
              functionName: Constants.GoogleSearchToolFunctionKey,
              functionDescription: "Search Google for current information on any topic",
              functionParameters: BinaryData.FromString("""
                     {
        "type": "object",
        "properties": {
            "query": {
                "type": "string",
                "description": "The search query to send to Google"
            },
            "maxResults": {
                "type": "integer",
                "description": "Maximum number of search results to return (default: 5)",
                "minimum": 4,
                "maximum": 10
            },
            "language": {
                "type": "string",
                "description": "Language code for search results (e.g., 'en', 'cs', 'de')",
                "default": "en"
            }
        },
        "required": ["query"]
    }
    """)
          );
        public async Task<string> SearchGoogle(string query, int maxResults = 5, string language = "en")
        {
            try
            {
                string apiKey = configuration[Constants.GoogleApiKey] ?? throw new InvalidOperationException("GoogleApiKey is not configured.");
                string searchEngineId = configuration[Constants.GoogleSearchEngineId] ?? throw new InvalidOperationException("GoogleSearchEngineId is not configured.");

                using var httpClient = new HttpClient();

                string encodedQuery = HttpUtility.UrlEncode(query);
                string url = $"https://www.googleapis.com/customsearch/v1?" +
                            $"key={apiKey}" +
                            $"&cx={searchEngineId}" +
                            $"&q={encodedQuery}" +
                            $"&num={Math.Min(maxResults, 10)}" +
                            $"&hl={language}";

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<GoogleSearchResponse>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });


                if (searchResult?.Items == null || searchResult.Items.Length == 0)
                {
                    return "No search results found for the query.";
                }

                var results = new System.Text.StringBuilder();
                results.AppendLine($"Search results for: {query}");
                results.AppendLine();

                for (int i = 0; i < searchResult.Items.Length; i++)
                {
                    var item = searchResult.Items[i];
                    results.AppendLine($"{i + 1}. {item.Title}");
                    results.AppendLine($"   URL: {item.Link}");
                    results.AppendLine($"   Snippet: {item.Snippet}");
                    results.AppendLine();
                }

                return results.ToString();
            }
            catch (HttpRequestException ex)
            {
                return $"Error performing search: {ex.Message}";
            }
            catch (JsonException ex)
            {
                return $"Error parsing search results: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Unexpected error: {ex.Message}";
            }
        }
    }
}
