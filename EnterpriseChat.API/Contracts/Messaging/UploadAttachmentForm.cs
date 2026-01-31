using Microsoft.AspNetCore.Http;

namespace EnterpriseChat.API.Contracts.Messaging;

public sealed class UploadAttachmentForm
{
    public IFormFile File { get; set; } = default!;
}
