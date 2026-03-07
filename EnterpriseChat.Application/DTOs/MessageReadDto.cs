using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.Enums;

public sealed class MessageReadDto
{
    public Guid Id { get; init; }
    public Guid RoomId { get; init; }
    public Guid SenderId { get; init; }
    public string Content { get; init; } = string.Empty;

        public MessageStatus GlobalStatus { get; init; }  
        public MessageStatus PersonalStatus { get; init; }

    public DateTime CreatedAt { get; init; }
    public List<MessageReceiptDto> Receipts { get; set; } = new();

        public int DeliveredCount { get; init; }
    public int ReadCount { get; init; }
    public int TotalRecipients { get; init; }
    public MessageReactionsDto? Reactions { get; init; }

    public bool IsEdited { get; init; }
    public bool IsDeleted { get; init; }
    public Guid? ReplyToMessageId { get; init; }
    public ReplyInfoDto? ReplyInfo { get; init; }
    public bool IsSystem { get; init; }
    public SystemMessageType? Type { get; init; }




}