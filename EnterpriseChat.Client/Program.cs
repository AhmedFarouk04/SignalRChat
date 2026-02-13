using EnterpriseChat.Client;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Authentication.Services;
using EnterpriseChat.Client.Services.Attachments;
using EnterpriseChat.Client.Services.Chat;
using EnterpriseChat.Client.Services.Http;
using EnterpriseChat.Client.Services.JsInterop;
using EnterpriseChat.Client.Services.Reaction;
using EnterpriseChat.Client.Services.Realtime;
using EnterpriseChat.Client.Services.Rooms;
using EnterpriseChat.Client.Services.Ui;
using EnterpriseChat.Client.ViewModels;
using EnterpriseChat.Domain.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<ITokenStore, LocalStorageTokenStore>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthStateProvider>());

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddScoped(sp =>
{
    var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7188";
    var client = new HttpClient
    {
        BaseAddress = new Uri(apiBase.EndsWith("/") ? apiBase : apiBase + "/"),
        Timeout = TimeSpan.FromSeconds(30)
    };
    return client;
});

builder.Services.AddScoped<IApiClient, ApiClient>();

builder.Services.AddScoped<IChatRealtimeClient, ChatRealtimeClient>();
builder.Services.AddScoped<IScrollService, ScrollService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<ChatViewModel>();
builder.Services.AddScoped<RoomsViewModel>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<AttachmentDownloadService>();
builder.Services.AddScoped<EnterpriseChat.Client.Authentication.AuthFlowState>();
builder.Services.AddScoped<AuthApi>();
builder.Services.AddScoped<UsersApi>();
builder.Services.AddScoped<ChatApi>();
builder.Services.AddScoped<GroupsApi>();
builder.Services.AddScoped<ModerationApi>();
builder.Services.AddScoped<AttachmentsApi>();
builder.Services.AddScoped<PresenceApi>();
builder.Services.AddSingleton<RoomFlagsStore>();
builder.Services.AddScoped<NotificationSoundService>();
builder.Services.AddScoped<EnterpriseChat.Client.Models.ReplyContext>();
builder.Services.AddScoped<ReactionsApi>();

builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.WebAssembly.Rendering", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components", LogLevel.Debug);

await builder.Build().RunAsync();