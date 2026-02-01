using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Application.DTOs;

public sealed class MessageDto
{
    public Guid Id { get; init; }
    public Guid RoomId { get; init; }
    public Guid SenderId { get; init; }
    public string Content { get; init; } = string.Empty;
    public MessageStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }

    public List<MessageReceiptDto> Receipts { get; set; } // userId + status + timestamp
}
