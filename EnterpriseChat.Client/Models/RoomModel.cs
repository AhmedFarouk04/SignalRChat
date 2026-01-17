namespace EnterpriseChat.Client.Models;

public sealed class RoomModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = "Group";

    public Guid? OtherUserId { get; init; }
    public string? OtherDisplayName { get; init; }

    public int UnreadCount { get; init; }
}
