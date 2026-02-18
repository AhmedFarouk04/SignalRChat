using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
namespace EnterpriseChat.Domain.Common;

public sealed class LastMessageInfo
{
    public RoomId RoomId { get; init; }
    public MessageId Id { get; init; }
    public UserId SenderId { get; init; }
    public string Content { get; init; } = "";
    public DateTime CreatedAt { get; init; }

    public int TotalRecipients { get; init; }
    public int DeliveredCount { get; init; }
    public int ReadCount { get; init; }
    public MessageStatus? PersonalStatus { get; init; }
}
