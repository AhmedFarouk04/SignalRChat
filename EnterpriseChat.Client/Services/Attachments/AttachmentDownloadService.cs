using EnterpriseChat.Client.Services.Http;
using Microsoft.JSInterop;

namespace EnterpriseChat.Client.Services.Attachments;

public sealed class AttachmentDownloadService
{
    private readonly IJSRuntime _js;

    public AttachmentDownloadService(IJSRuntime js)
    {
        _js = js;
    }

    // ✅ ده اللي هنستخدمه من ChatAttachments
    public async Task DownloadAsync(IApiClient.ApiFile file)
    {
        await _js.InvokeVoidAsync(
            "downloadFileFromStream",
            file.FileName,
            file.ContentType,
            file.Bytes
        );
    }
}
