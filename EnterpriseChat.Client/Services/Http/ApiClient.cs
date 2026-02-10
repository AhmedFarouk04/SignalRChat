using EnterpriseChat.Client.Authentication.Abstractions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using static EnterpriseChat.Client.Services.Http.IApiClient;

namespace EnterpriseChat.Client.Services.Http;

public sealed class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ITokenStore _tokenStore;

    public ApiClient(HttpClient http, ITokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    private async Task AttachTokenAsync()
    {
        var token = await _tokenStore.GetAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            // مهم: بعد Logout أو clear token
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        await AttachTokenAsync();

        using var res = await _http.GetAsync(url, ct);
        await EnsureSuccessOrThrow(res);

        if (res.StatusCode == HttpStatusCode.NoContent)
            return default;

        return await res.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }
    public async Task<byte[]> GetBytesAsync(string url, CancellationToken ct = default)
    {
        await AttachTokenAsync();


        using var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessOrThrow(res);
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default)
    {
        await AttachTokenAsync();

        using var res = await _http.PostAsJsonAsync(url, body, JsonOptions, ct);
        await EnsureSuccessOrThrow(res);

        if (res.StatusCode == HttpStatusCode.NoContent)
            return default;

        return await res.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
    }

    public async Task PostAsync(string url, CancellationToken ct = default)
    {
        await AttachTokenAsync();

        using var res = await _http.PostAsync(url, content: null, ct);
        await EnsureSuccessOrThrow(res);
    }

    public async Task PutAsync<TRequest>(string url, TRequest body, CancellationToken ct = default)
    {
        await AttachTokenAsync();

        using var res = await _http.PutAsJsonAsync(url, body, JsonOptions, ct);
        await EnsureSuccessOrThrow(res);
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        await AttachTokenAsync();

        using var res = await _http.DeleteAsync(url, ct);
        await EnsureSuccessOrThrow(res);
    }

    public async Task<TResponse?> PostMultipartAsync<TResponse>(
        string url,
        string fieldName,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        await AttachTokenAsync();

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        form.Add(fileContent, fieldName, fileName);

        using var res = await _http.PostAsync(url, form, ct);
        await EnsureSuccessOrThrow(res);

        if (res.StatusCode == HttpStatusCode.NoContent)
            return default;

        return await res.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
    }

    public async Task<HttpStatusCode> SendAsync(HttpMethod method, string url, HttpContent? content = null, CancellationToken ct = default)
    {
        await AttachTokenAsync();

        using var req = new HttpRequestMessage(method, url) { Content = content };
        using var res = await _http.SendAsync(req, ct);
        await EnsureSuccessOrThrow(res);

        return res.StatusCode;
    }

    private static async Task EnsureSuccessOrThrow(HttpResponseMessage res)
    {
        if (res.IsSuccessStatusCode)
            return;

        string body = "";
        try { body = await res.Content.ReadAsStringAsync(); } catch { }

        var msg = $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}";
        if (!string.IsNullOrWhiteSpace(body))
        {
            if (body.Length > 1200) body = body[..1200] + "…";
            msg += $": {body}";
        }

        throw new HttpRequestException(msg, null, res.StatusCode);
    }
    public async Task<ApiFile> GetFileAsync(string url, CancellationToken ct = default)
    {
        await AttachTokenAsync();


        using var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessOrThrow(res);

        var bytes = await res.Content.ReadAsByteArrayAsync(ct);

        var ctHeader = res.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

        var fileName =
            res.Content.Headers.ContentDisposition?.FileNameStar
            ?? res.Content.Headers.ContentDisposition?.FileName
            ?? "download";

        fileName = fileName.Trim('"');

        return new ApiFile(bytes, fileName, ctHeader);
    }

    public async Task<TResponse?> PatchAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default)
    {
        // فك التشفير باستخدام JsonSerializer الافتراضي لو _options مش متاحة
        var json = System.Text.Json.JsonSerializer.Serialize(body);

        // التصحيح: ترتيب الباراميترز (Content, Encoding, MediaType)
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // استخدام SendAsync الموجودة عندك
        await SendAsync(HttpMethod.Patch, url, content, ct);

        return default;
    }
}
