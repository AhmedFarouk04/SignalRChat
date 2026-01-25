using EnterpriseChat.Client;
using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Authentication.Services;
using EnterpriseChat.Client.Services.Chat;
using EnterpriseChat.Client.Services.Http;
using EnterpriseChat.Client.Services.JsInterop;
using EnterpriseChat.Client.Services.Realtime;
using EnterpriseChat.Client.Services.Rooms;
using EnterpriseChat.Client.Services.Ui;
using EnterpriseChat.Client.ViewModels;
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
    // prefer config if present
    var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001";
    return new HttpClient { BaseAddress = new Uri(apiBase.EndsWith("/") ? apiBase : apiBase + "/") };
});

builder.Services.AddScoped<IApiClient, ApiClient>();

builder.Services.AddScoped<IChatRealtimeClient, ChatRealtimeClient>();
builder.Services.AddScoped<IScrollService, ScrollService>();

builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<ChatViewModel>();
builder.Services.AddScoped<RoomsViewModel>();
builder.Services.AddScoped<ToastService>();

await builder.Build().RunAsync();
