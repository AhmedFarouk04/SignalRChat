namespace EnterpriseChat.API.Contracts.Messaging;

public sealed class SendMessageRequest
{
    public Guid RoomId { get; init; }
    public string Content { get; init; } = string.Empty;
    public Guid? ReplyToMessageId { get; init; } // ✅ أضف السطر ده
}
