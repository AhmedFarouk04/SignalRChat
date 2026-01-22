namespace EnterpriseChat.API.Contracts.Messaging;

public sealed class MarkRoomReadRequest
{
    public Guid LastMessageId { get; set; }
}
