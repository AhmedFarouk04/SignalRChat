using EnterpriseChat.Client;
using EnterpriseChat.Client.Authentication;
using EnterpriseChat.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<AuthStateProvider>());

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<ChatHubClient>();

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri("https://localhost:5001/") });

await builder.Build().RunAsync();
