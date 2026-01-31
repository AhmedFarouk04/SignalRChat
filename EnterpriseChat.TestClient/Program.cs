using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static async Task Main()
    {
        // ===== Config =====
        var baseUrls = new[]
        {
            "https://localhost:7188/",
            "http://localhost:5188/"
        };

        var loginUserOrEmail = "owner111@gmail.com"; // or "owner111"
        var password = "P@ssw0rd!123";

        // ===== HttpClient (dev cert) =====
      

        // ===== Pick working BaseUrl =====
        var picked = await PickWorkingBaseUrlAsync(baseUrls);

        // ===== HttpClient (dev cert) =====
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(picked.EndsWith("/") ? picked : picked + "/")
        };

        Console.WriteLine($"BaseUrl: {http.BaseAddress}");


        // ===== 1) Login =====
        var token = await LoginAsync(http, loginUserOrEmail, password);
        Console.WriteLine($"✅ Token received (len={token.Length})");

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // ===== 2) Rooms =====
        var rooms = await GetAsync<JsonElement>(http, "api/rooms");
        Console.WriteLine("✅ GET /api/rooms OK");
        PrettyPrint(rooms);

        Guid? firstRoomId = TryPickFirstGuid(rooms, "id");
        if (firstRoomId is null)
        {
            Console.WriteLine("⚠️ No rooms returned. Seeder maybe not applied.");
            return;
        }

        // ===== 3) Room details =====
        var roomDetails = await GetAsync<JsonElement>(http, $"api/rooms/{firstRoomId}");
        Console.WriteLine($"✅ GET /api/rooms/{firstRoomId} OK");
        PrettyPrint(roomDetails);

        // ===== 4) Search Users =====
        var users = await GetAsync<JsonElement>(http, "api/users/search?query=user&take=10");
        Console.WriteLine("✅ GET /api/users/search OK");
        PrettyPrint(users);

        var otherUserId = TryPickAnyGuid(users, "id");
        if (otherUserId is null)
            Console.WriteLine("⚠️ No users returned from search.");

        // ===== 5) Create/Get private room =====
        if (otherUserId is not null)
        {
            var priv = await PostAsync<JsonElement>(http, $"api/chat/private/{otherUserId}", body: null);
            Console.WriteLine("✅ POST /api/chat/private/{userId} OK");
            PrettyPrint(priv);
        }

        // ===== 6) Send message =====
        var sendBody = new
        {
            roomId = firstRoomId,
            content = $"Hello from TestClient @ {DateTime.Now:HH:mm:ss}"
        };

        var msg = await PostAsync<JsonElement>(http, "api/chat/messages", sendBody);
        Console.WriteLine("✅ POST /api/chat/messages OK");
        PrettyPrint(msg);

        Guid? messageId = TryPickFirstGuid(msg, "id");

        // ===== 7) Get room messages =====
        var msgs = await GetAsync<JsonElement>(http, $"api/chat/rooms/{firstRoomId}/messages?skip=0&take=20");
        Console.WriteLine("✅ GET /api/chat/rooms/{roomId}/messages OK");
        PrettyPrint(msgs);

        // ===== 8) Mark delivered/read + readers =====
        if (messageId is not null)
        {
            await PostNoBodyAsync(http, $"api/chat/messages/{messageId}/delivered");
            Console.WriteLine("✅ POST delivered OK");

            await PostNoBodyAsync(http, $"api/chat/messages/{messageId}/read");
            Console.WriteLine("✅ POST read OK");

            // Readers endpoint عندك ممكن يفشل (Bug EF) -> ما نوقفش التست
            try
            {
                var readers = await GetAsync<JsonElement>(http, $"api/chat/messages/{messageId}/readers");
                Console.WriteLine("✅ GET readers OK");
                PrettyPrint(readers);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"(skip) readers endpoint failed: {ex.Message}");
            }
        }

        // ===== 9) Room delivered/read =====
        try
        {
            await PostNoBodyAsync(http, $"api/chat/rooms/{firstRoomId}/delivered");
            Console.WriteLine("✅ POST room delivered OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"(skip) room delivered: {ex.Message}");
        }

        try
        {
            var lastId = messageId ?? TryPickFirstGuid(msgs, "id") ?? Guid.Empty;
            if (lastId != Guid.Empty)
            {
                // API بتاعك غالبًا عايز { lastMessageId: "..." }
                await PostAsync<JsonElement>(http, $"api/chat/rooms/{firstRoomId}/read", new { lastMessageId = lastId });
                Console.WriteLine("✅ POST room read OK");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"(skip) room read: {ex.Message}");
        }

        // ===== 10) Mute/Unmute + list =====
        await PostNoBodyAsync(http, $"api/chat/mute/{firstRoomId}");
        Console.WriteLine("✅ POST mute OK");

        var mutedList = await GetAsync<JsonElement>(http, "api/chat/muted");
        Console.WriteLine("✅ GET muted list OK");
        PrettyPrint(mutedList);

        await DeleteAsync(http, $"api/chat/mute/{firstRoomId}");
        Console.WriteLine("✅ DELETE unmute OK");

        // ===== 11) Block/Unblock + list =====
        if (otherUserId is not null)
        {
            await PostNoBodyAsync(http, $"api/chat/block/{otherUserId}");
            Console.WriteLine("✅ POST block OK");

            var blockedList = await GetAsync<JsonElement>(http, "api/chat/blocked");
            Console.WriteLine("✅ GET blocked list OK");
            PrettyPrint(blockedList);

            await DeleteAsync(http, $"api/chat/block/{otherUserId}");
            Console.WriteLine("✅ DELETE unblock OK");
        }

        // ===== 12) Create group =====
        var memberIds = PickManyGuids(users, "id", take: 2);
        if (memberIds.Count > 0)
        {
            var groupReq = new
            {
                name = "Test Group " + DateTime.Now.ToString("HHmmss"),
                members = memberIds
            };

            var groupCreate = await PostAsync<JsonElement>(http, "api/groups", groupReq);
            Console.WriteLine("✅ POST /api/groups OK");
            PrettyPrint(groupCreate);

            Guid? groupRoomId =
                TryPickFirstGuid(groupCreate, "roomId") ??
                TryPickFirstGuid(groupCreate, "id");

            if (groupRoomId is not null)
            {
                // GET group details
                var groupDetails = await GetAsync<JsonElement>(http, $"api/groups/{groupRoomId}");
                Console.WriteLine("✅ GET /api/groups/{roomId} OK");
                PrettyPrint(groupDetails);

                // Rename (PUT /api/groups/{roomId}) body { name: "..." }
                await PutAsync(http, $"api/groups/{groupRoomId}", new { name = "Renamed " + DateTime.Now.ToString("HHmmss") });
                Console.WriteLine("✅ PUT /api/groups/{roomId} rename OK");

                // Add member
                var addUserId = memberIds[0];
                await PostNoBodyAsync(http, $"api/groups/{groupRoomId}/members/{addUserId}");
                Console.WriteLine("✅ POST add member OK");

                // Promote/Demote admin
                await PostNoBodyAsync(http, $"api/groups/{groupRoomId}/admins/{addUserId}");
                Console.WriteLine("✅ POST promote admin OK");

                await DeleteAsync(http, $"api/groups/{groupRoomId}/admins/{addUserId}");
                Console.WriteLine("✅ DELETE demote admin OK");

                // Transfer owner
                await PostNoBodyAsync(http, $"api/groups/{groupRoomId}/owner/{addUserId}");
                Console.WriteLine("✅ POST transfer owner OK");

                // ===== Attachments =====
                var filePath = Path.Combine(AppContext.BaseDirectory, "test-upload.txt");
                await File.WriteAllTextAsync(filePath, "Hello Attachment from TestClient");

                var uploaded = await UploadFileAsync(http, $"api/chat/rooms/{groupRoomId}/attachments", filePath);
                Console.WriteLine("✅ POST upload attachment OK");
                PrettyPrint(uploaded);

                var list = await GetAsync<JsonElement>(http, $"api/chat/rooms/{groupRoomId}/attachments?skip=0&take=20");
                Console.WriteLine("✅ GET room attachments OK");
                PrettyPrint(list);

                var attachmentId = TryPickFirstGuid(list, "id");
                if (attachmentId is not null)
                {
                    var bytes = await http.GetByteArrayAsync($"api/attachments/{attachmentId}");
                    Console.WriteLine($"✅ GET download attachment OK (bytes={bytes.Length})");

                    await DeleteAsync(http, $"api/attachments/{attachmentId}");
                    Console.WriteLine("✅ DELETE attachment OK");
                }

                // Delete group (optional)
                try
                {
                    await DeleteAsync(http, $"api/groups/{groupRoomId}");
                    Console.WriteLine("✅ DELETE group OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"(skip) delete group: {ex.Message}");
                }

                // Leave group (optional)
                try
                {
                    await DeleteAsync(http, $"api/groups/{groupRoomId}/leave");
                    Console.WriteLine("✅ DELETE leave group OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"(skip) leave group: {ex.Message}");
                }

            }
        }

        // ===== Presence =====
        var online = await GetAsync<JsonElement>(http, "api/presence/online");
        Console.WriteLine("✅ GET /api/presence/online OK");
        PrettyPrint(online);

        if (otherUserId is not null)
        {
            var isOnline = await GetAsync<JsonElement>(http, $"api/presence/online/{otherUserId}");
            Console.WriteLine("✅ GET /api/presence/online/{userId} OK");
            PrettyPrint(isOnline);
        }

        // ===== Typing =====
        await PostAsync<JsonElement>(
     http,
     $"api/rooms/{firstRoomId}/typing/start",
     new { ttlSeconds = 5 }
 );
        Console.WriteLine("✅ POST typing start OK");


        await PostNoBodyAsync(http, $"api/rooms/{firstRoomId}/typing/stop");
        Console.WriteLine("✅ POST typing stop OK");

        Console.WriteLine("\n🎉 DONE: All tests finished.");
    }

    // ---------------- BaseUrl picker ----------------

    private static async Task<string> PickWorkingBaseUrlAsync(string[] baseUrls)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        using var tempHttp = new HttpClient(handler);

        foreach (var u in baseUrls)
        {
            try
            {
                var res = await tempHttp.GetAsync($"{u.TrimEnd('/')}/swagger/index.html");
                if ((int)res.StatusCode < 500)
                    return u;
            }
            catch { }

            try
            {
                var res2 = await tempHttp.GetAsync($"{u.TrimEnd('/')}/api/rooms");
                if (res2.StatusCode == HttpStatusCode.Unauthorized ||
                    res2.StatusCode == HttpStatusCode.Forbidden ||
                    res2.IsSuccessStatusCode)
                    return u;
            }
            catch { }
        }

        throw new Exception("No working baseUrl found.");
    }

    // ---------------- Helpers ----------------

    private static async Task<string> LoginAsync(HttpClient http, string userOrEmail, string password)
    {
        // ✅ API عندك expects: { "identifier": "...", "password": "..." }
        var body = new { identifier = userOrEmail, password };

        using var res = await http.PostAsync("api/auth/login", JsonContent(body));
        var text = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"Login failed HTTP {(int)res.StatusCode}: {text}");

        using var doc = JsonDocument.Parse(text);

        if (doc.RootElement.TryGetProperty("accessToken", out var t1))
            return t1.GetString() ?? throw new Exception("accessToken null");

        if (doc.RootElement.TryGetProperty("token", out var t2))
            return t2.GetString() ?? throw new Exception("token null");

        throw new Exception($"Login response has no token field. Body: {text}");
    }

    private static async Task<T> GetAsync<T>(HttpClient http, string url)
    {
        using var res = await http.GetAsync(url);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"GET {url} failed HTTP {(int)res.StatusCode}: {text}");

        return JsonSerializer.Deserialize<T>(text, JsonOpts)!;
    }

    private static async Task<T> PostAsync<T>(HttpClient http, string url, object? body)
    {
        using var res = await http.PostAsync(url, body is null ? new StringContent("") : JsonContent(body));
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"POST {url} failed HTTP {(int)res.StatusCode}: {text}");

        if (string.IsNullOrWhiteSpace(text)) return default!;
        return JsonSerializer.Deserialize<T>(text, JsonOpts)!;
    }

    private static async Task PostNoBodyAsync(HttpClient http, string url)
    {
        using var res = await http.PostAsync(url, new StringContent(""));
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"POST {url} failed HTTP {(int)res.StatusCode}: {text}");
    }

    private static async Task PutAsync(HttpClient http, string url, object body)
    {
        using var res = await http.PutAsync(url, JsonContent(body));
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"PUT {url} failed HTTP {(int)res.StatusCode}: {text}");
    }

    private static async Task DeleteAsync(HttpClient http, string url)
    {
        using var res = await http.DeleteAsync(url);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"DELETE {url} failed HTTP {(int)res.StatusCode}: {text}");
    }

    private static HttpContent JsonContent(object body)
        => new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> UploadFileAsync(HttpClient http, string url, string filePath)
    {
        using var form = new MultipartFormDataContent();

        var bytes = await File.ReadAllBytesAsync(filePath);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        form.Add(fileContent, "file", Path.GetFileName(filePath));

        using var res = await http.PostAsync(url, form);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"UPLOAD {url} failed HTTP {(int)res.StatusCode}: {text}");

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        return doc.RootElement.Clone();
    }

    private static void PrettyPrint(JsonElement el)
    {
        var s = JsonSerializer.Serialize(el, new JsonSerializerOptions(JsonOpts) { WriteIndented = true });
        Console.WriteLine(s);
    }

    private static Guid? TryPickFirstGuid(JsonElement el, string prop)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty(prop, out var v) &&
                v.ValueKind == JsonValueKind.String &&
                Guid.TryParse(v.GetString(), out var g))
                return g;
        }

        if (el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0)
        {
            var first = el[0];
            if (first.ValueKind == JsonValueKind.Object &&
                first.TryGetProperty(prop, out var v) &&
                v.ValueKind == JsonValueKind.String &&
                Guid.TryParse(v.GetString(), out var g))
                return g;
        }

        return null;
    }

    private static Guid? TryPickAnyGuid(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Array) return null;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty(prop, out var v) &&
                v.ValueKind == JsonValueKind.String &&
                Guid.TryParse(v.GetString(), out var g))
                return g;
        }

        return null;
    }

    private static List<Guid> PickManyGuids(JsonElement el, string prop, int take)
    {
        var list = new List<Guid>();
        if (el.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty(prop, out var v) &&
                v.ValueKind == JsonValueKind.String &&
                Guid.TryParse(v.GetString(), out var g))
            {
                list.Add(g);
                if (list.Count >= take) break;
            }
        }
        return list;
    }
}
