namespace EnterpriseChat.Application.DTOs;

public sealed class SendMessageWithReplyRequest
{
    public Guid RoomId { get; set; }
    public string Content { get; set; } = "";
    public Guid? ReplyToMessageId { get; set; }
    public ReplyInfoDto? ReplyInfo { get; set; }
}