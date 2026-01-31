using EnterpriseChat.Application.DTOs;

namespace EnterpriseChat.Client.Services.Http;

public sealed class AttachmentsApi
{
    private readonly IApiClient _api;

    public AttachmentsApi(IApiClient api)
    {
        _api = api;
    }

    public Task<AttachmentDto?> UploadAsync(
        Guid roomId,
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        // field name غالباً "file"
        return _api.PostMultipartAsync<AttachmentDto>(
            ApiEndpoints.UploadAttachment(roomId),
            fieldName: "file",
            content: stream,
            fileName: fileName,
            contentType: string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            ct: ct);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid roomId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await _api.GetAsync<IReadOnlyList<AttachmentDto>>(ApiEndpoints.ListRoomAttachments(roomId, skip, take), ct) ?? [];

    public Task DeleteAsync(Guid attachmentId, CancellationToken ct = default)
        => _api.DeleteAsync(ApiEndpoints.DeleteAttachment(attachmentId), ct);

    public Task<IApiClient.ApiFile> DownloadAsync(Guid attachmentId, CancellationToken ct = default)
    => _api.GetFileAsync(ApiEndpoints.DownloadAttachment(attachmentId), ct);

}
