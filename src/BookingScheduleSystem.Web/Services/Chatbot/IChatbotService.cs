using BookingScheduleSystem.Web.Models;

namespace BookingScheduleSystem.Web.Services.Chatbot;

public interface IChatbotService
{
    Task InitializeAsync(Guid tenantId, string tenantName, string tenantSlug);
    void SetLanguagePreference(string language);
    Task<ChatMessage> SendMessageAsync(string userMessage);
    IReadOnlyList<ChatMessage> GetConversationHistory();
    void Reset();
}
