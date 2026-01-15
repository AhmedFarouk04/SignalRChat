namespace EnterpriseChat.Client.Models;

public sealed class MessageModel
{
    public Guid Id { get; set; }              // server id (بعد النجاح)
    public Guid? ClientId { get; set; }       // optimistic id
    public Guid RoomId { get; init; }
    public Guid SenderId { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    public MessageStatus Status { get; set; } // Pending/Sent/Failed/Delivered/Read
    public string? Error { get; set; }        // لو فشلت
}
