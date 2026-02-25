namespace BookingScheduleSystem.Web.Models;

public sealed class ChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsToolAction { get; init; }

    public static class Roles
    {
        public const string User = "user";
        public const string Assistant = "assistant";
        public const string System = "system";
    }
}
