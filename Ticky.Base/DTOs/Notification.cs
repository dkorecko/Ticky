namespace Ticky.Base.DTOs;

public record Notification(string text, NotificationType type = NotificationType.Success)
{
    public NotificationType Type { get; set; } = type;
    public string Text { get; set; } = text;
}
