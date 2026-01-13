namespace EnterpriseChat.Application.DTOs;

public sealed class MessageReadReceiptDto
{
    public Guid UserId { get; init; }
    public DateTime ReadAt { get; init; }
}
