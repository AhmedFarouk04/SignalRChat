using EnterpriseChat.API.Hubs;
using EnterpriseChat.API.Messaging;
using EnterpriseChat.API.Middleware;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Infrastructure;
using EnterpriseChat.Infrastructure.Persistence;
using EnterpriseChat.Infrastructure.Persistence.Seeding;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EnterpriseChat.API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please insert JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// DbContext
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
         sql => sql.CommandTimeout(300)));

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// MediatR
builder.Services.AddMediatR(typeof(SendMessageCommand).Assembly);

// Redis
var redisConnectionString = builder.Configuration["Presence:Redis"];
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    throw new InvalidOperationException("Presence:Redis is not configured");
}
builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConnectionString));

// SignalR
builder.Services.AddSignalR()
    .AddStackExchangeRedis(
        builder.Configuration["Presence:Redis"]!,
        options =>
        {
            options.Configuration.ChannelPrefix = "EnterpriseChat";
            options.Configuration.AbortOnConnectFail = false;
        });
builder.Services.AddScoped<IMessageBroadcaster, SignalRMessageBroadcaster>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),

            NameClaimType = "sub",
            RoleClaimType = "role"
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },

            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                logger.LogError("JWT Authentication Failed!");
                logger.LogError("Exception Message: {Message}", context.Exception?.Message);
                logger.LogError("Exception Type: {Type}", context.Exception?.GetType().Name);
                logger.LogError("Token received: {Token}",
                    context.Request.Headers["Authorization"].ToString() ?? "No Authorization header");

                if (context.Exception?.InnerException != null)
                {
                    logger.LogError("Inner Exception: {Inner}", context.Exception.InnerException.Message);
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n========== JWT AUTH FAILED ==========");
                Console.WriteLine("Message: " + (context.Exception?.Message ?? "No message"));
                Console.WriteLine("Token: " + (context.Request.Headers["Authorization"].ToString() ?? "No token"));
                Console.WriteLine("====================================\n");
                Console.ResetColor();

                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("JWT Token VALIDATED Successfully!");
                logger.LogInformation("User ID: {Sub}", context.Principal?.FindFirst("sub")?.Value);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("JWT Token VALIDATED Successfully!");
                Console.ResetColor();

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    await db.Database.MigrateAsync();
    var seeder = new DatabaseSeeder(db);
    await seeder.SeedAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();