// EnterpriseChat.Application/DTOs/ReplyInfoDto.cs
namespace EnterpriseChat.Application.DTOs;

public sealed class ReplyInfoDto
{
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string ContentPreview { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

