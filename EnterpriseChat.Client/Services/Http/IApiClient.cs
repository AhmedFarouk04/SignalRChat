using System.Net;

namespace EnterpriseChat.Client.Services.Http;

public interface IApiClient
{
    Task<T?> GetAsync<T>(string url, CancellationToken ct = default);

    Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default);
    Task PostAsync(string url, CancellationToken ct = default);

    Task PutAsync<TRequest>(string url, TRequest body, CancellationToken ct = default);

    Task DeleteAsync(string url, CancellationToken ct = default);

    Task<TResponse?> PostMultipartAsync<TResponse>(
        string url,
        string fieldName,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default);
    Task<ApiFile> GetFileAsync(string url, CancellationToken ct = default);

    public sealed record ApiFile(byte[] Bytes, string FileName, string ContentType);
    Task<byte[]> GetBytesAsync(string url, CancellationToken ct = default);

    Task<HttpStatusCode> SendAsync(HttpMethod method, string url, HttpContent? content = null, CancellationToken ct = default);
}
