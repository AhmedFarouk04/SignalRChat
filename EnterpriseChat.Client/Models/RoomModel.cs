namespace EnterpriseChat.Client.Models;

public sealed class RoomModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
