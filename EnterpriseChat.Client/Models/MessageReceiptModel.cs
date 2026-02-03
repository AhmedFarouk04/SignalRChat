namespace EnterpriseChat.Client.Models;

public sealed class MessageReceiptModel
{
    public Guid UserId { get; set; }
    public MessageStatus Status { get; set; }
}
