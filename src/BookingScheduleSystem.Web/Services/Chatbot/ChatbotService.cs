using System.Text;
using System.Text.Json;
using BookingScheduleSystem.Web.Models;

namespace BookingScheduleSystem.Web.Services.Chatbot;

public sealed class ChatbotService : IChatbotService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ChatbotToolExecutor _toolExecutor;
    private readonly ILogger<ChatbotService> _logger;

    private readonly List<ChatMessage> _displayHistory = new();
    private readonly List<JsonElement> _apiMessages = new();
    private Guid _tenantId;
    private string _tenantName = string.Empty;
    private string _tenantSlug = string.Empty;
    private bool _initialized;

    private const int MaxToolIterations = 10;
    private const int MaxMessages = 50;
    private const int TrimThreshold = 30;
    private const int TrimKeepCount = 20;

    public ChatbotService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ChatbotToolExecutor toolExecutor,
        ILogger<ChatbotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _toolExecutor = toolExecutor;
        _logger = logger;
    }

    public Task InitializeAsync(Guid tenantId, string tenantName, string tenantSlug)
    {
        _tenantId = tenantId;
        _tenantName = tenantName;
        _tenantSlug = tenantSlug;
        _initialized = true;
        _displayHistory.Clear();
        _apiMessages.Clear();
        return Task.CompletedTask;
    }

    public async Task<ChatMessage> SendMessageAsync(string userMessage)
    {
        if (!_initialized)
            throw new InvalidOperationException("Chatbot not initialized. Call InitializeAsync first.");

        if (_displayHistory.Count >= MaxMessages)
        {
            return new ChatMessage
            {
                Role = ChatMessage.Roles.Assistant,
                Content = "This conversation has reached its limit. Please refresh the page to start a new conversation."
            };
        }

        // Add user message to display history
        _displayHistory.Add(new ChatMessage
        {
            Role = ChatMessage.Roles.User,
            Content = userMessage
        });

        // Add user message to API messages
        var userApiMsg = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(new { role = "user", content = userMessage }));
        _apiMessages.Add(userApiMsg);

        // Trim if needed
        TrimConversationIfNeeded();

        // Call Claude with tool-use loop
        var assistantContent = await CallClaudeWithToolLoopAsync();

        var assistantMessage = new ChatMessage
        {
            Role = ChatMessage.Roles.Assistant,
            Content = assistantContent
        };
        _displayHistory.Add(assistantMessage);

        return assistantMessage;
    }

    public IReadOnlyList<ChatMessage> GetConversationHistory() => _displayHistory.AsReadOnly();

    public void Reset()
    {
        _displayHistory.Clear();
        _apiMessages.Clear();
        _initialized = false;
    }

    private async Task<string> CallClaudeWithToolLoopAsync()
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        var model = _configuration["Anthropic:Model"] ?? "claude-haiku-4-5-20251001";

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Anthropic API key not configured");
            return "I'm sorry, the booking assistant is not configured yet. Please contact the business directly to book an appointment.";
        }

        var httpClient = _httpClientFactory.CreateClient();

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            var requestBody = BuildApiRequest(model);

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Anthropic API");
                return "I'm having trouble connecting right now. Please try again in a moment.";
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Anthropic API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                return "I encountered an issue. Please try again.";
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Check stop reason
            var stopReason = responseJson.GetProperty("stop_reason").GetString();
            var contentBlocks = responseJson.GetProperty("content");

            if (stopReason == "tool_use")
            {
                // Process tool calls
                var assistantApiMsg = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(new
                    {
                        role = "assistant",
                        content = contentBlocks
                    }));
                _apiMessages.Add(assistantApiMsg);

                var toolResults = new List<object>();

                foreach (var block in contentBlocks.EnumerateArray())
                {
                    if (block.GetProperty("type").GetString() == "tool_use")
                    {
                        var toolName = block.GetProperty("name").GetString()!;
                        var toolInput = block.GetProperty("input");
                        var toolUseId = block.GetProperty("id").GetString()!;

                        _logger.LogInformation("Executing chatbot tool: {ToolName}", toolName);

                        var toolResult = await _toolExecutor.ExecuteToolAsync(toolName, toolInput, _tenantId);

                        toolResults.Add(new
                        {
                            type = "tool_result",
                            tool_use_id = toolUseId,
                            content = toolResult
                        });
                    }
                }

                // Add tool results as user message
                var toolResultMsg = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(new { role = "user", content = toolResults }));
                _apiMessages.Add(toolResultMsg);

                // Continue loop — Claude will process the tool results
                continue;
            }

            // stop_reason == "end_turn" or other — extract text response
            var textContent = ExtractTextFromContent(contentBlocks);

            // Add assistant message to API messages
            var finalAssistantMsg = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(new
                {
                    role = "assistant",
                    content = contentBlocks
                }));
            _apiMessages.Add(finalAssistantMsg);

            return textContent;
        }

        return "I've been working on your request but it's taking longer than expected. Could you try rephrasing your question?";
    }

    private string BuildApiRequest(string model)
    {
        var systemPrompt = BuildSystemPrompt();
        var tools = ChatbotToolDefinitions.GetToolDefinitions();

        var requestObj = new
        {
            model,
            max_tokens = 1024,
            system = systemPrompt,
            tools,
            messages = _apiMessages
        };

        return JsonSerializer.Serialize(requestObj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    private string BuildSystemPrompt()
    {
        // Use Philippine time (UTC+8) since the app serves PH users
        var pht = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));

        return $"""
            You are a friendly, helpful booking assistant for "{_tenantName}". Your job is to help visitors check availability and book appointments.

            IMPORTANT RULES:
            - Be concise and friendly. Use short responses.
            - When showing available slots, format them clearly with date, time, and provider name.
            - Never ask the user for a password — you auto-generate one during registration.
            - Guide the user through this flow: check availability → pick a slot → collect name & contact → send OTP → verify OTP → register → create booking.
            - You can skip straight to checking availability if the user asks.
            - If the user already knows what they want, don't over-explain. Just help them book.
            - For the OTP step, tell the user to check their email/phone for a 6-digit code.
            - After booking, share the confirmation details (date, time, provider).
            - If check_availability returns "available_slots" as empty but "free_time" windows exist, it means the provider is open during those hours but hasn't created specific bookable slots yet. Tell the user the provider's working hours and suggest they contact the business directly or try again later.
            - If there are truly no available slots and no free time, suggest the user try different dates.
            - You can only help with booking at "{_tenantName}". For other questions, politely redirect.
            - Today's date is {pht:yyyy-MM-dd} ({pht:dddd}). Use this when calculating dates like "this week", "tomorrow", etc.
            """;
    }

    private static string ExtractTextFromContent(JsonElement contentBlocks)
    {
        var sb = new StringBuilder();

        foreach (var block in contentBlocks.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "text")
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(block.GetProperty("text").GetString());
            }
        }

        return sb.Length > 0 ? sb.ToString() : "I'm here to help! Ask me about available appointment times.";
    }

    private void TrimConversationIfNeeded()
    {
        if (_apiMessages.Count > TrimThreshold)
        {
            var toRemove = _apiMessages.Count - TrimKeepCount;
            _apiMessages.RemoveRange(0, toRemove);

            // Ensure first message is from user (API requirement)
            while (_apiMessages.Count > 0)
            {
                var first = _apiMessages[0];
                if (first.TryGetProperty("role", out var role) && role.GetString() == "user")
                    break;
                _apiMessages.RemoveAt(0);
            }
        }
    }
}
