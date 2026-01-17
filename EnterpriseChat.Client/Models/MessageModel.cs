namespace EnterpriseChat.Client.Models;

public sealed class MessageModel
{
    public Guid Id { get; set; }              
    public Guid? ClientId { get; set; }       
    public Guid RoomId { get; init; }
    public Guid SenderId { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    public MessageStatus Status { get; set; } 
    public string? Error { get; set; }       
}
