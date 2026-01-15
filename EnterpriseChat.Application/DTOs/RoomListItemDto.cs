namespace EnterpriseChat.Application.DTOs;

public sealed class RoomListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;

    // Private chat only
    public Guid? OtherUserId { get; init; }
    public string? OtherDisplayName { get; init; }
}
