using EnterpriseChat.API.Auth;
using EnterpriseChat.API.Hubs;
using EnterpriseChat.API.Messaging;
using EnterpriseChat.API.Middleware;
using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces; // IPasswordHasher (Application)
using EnterpriseChat.Infrastructure;
using EnterpriseChat.Infrastructure.Persistence;
using EnterpriseChat.Infrastructure.Persistence.Seeding;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }.AsSecurityScheme(),
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("client", p =>
        p.AllowAnyHeader()
         .AllowAnyMethod()
         .SetIsOriginAllowed(_ => true)); // بدون AllowCredentials
});


// DbContext
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.CommandTimeout(300)));

// Infrastructure + MediatR
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediatR(typeof(SendMessageCommand).Assembly);

// JWT
builder.Services.AddSingleton<JwtTokenService>();

// Auth (MemoryCache + Hasher + AuthService)
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ✅ SMTP Options (bind من Email:Smtp)
builder.Services.AddOptions<SmtpSettings>()
    .Bind(builder.Configuration.GetRequiredSection("Email:Smtp"))
    .Validate(s => !string.IsNullOrWhiteSpace(s.Host), "Email:Smtp:Host is required")
    .Validate(s => !string.IsNullOrWhiteSpace(s.Username), "Email:Smtp:Username is required")
    .Validate(s => !string.IsNullOrWhiteSpace(s.Password), "Email:Smtp:Password is required")
    .Validate(s => !string.IsNullOrWhiteSpace(s.FromEmail), "Email:Smtp:FromEmail is required")
    .ValidateOnStart();

builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();


// Redis
var redisConnectionString =
    builder.Configuration["Presence:Redis"]
    ?? builder.Configuration.GetConnectionString("Redis");

if (string.IsNullOrWhiteSpace(redisConnectionString))
    throw new InvalidOperationException("Redis is not configured. Set Presence:Redis OR ConnectionStrings:Redis");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, EnterpriseChat.API.Auth.SubUserIdProvider>();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // مكانها الصحيح هنا في الـ Hub options
})
.AddStackExchangeRedis(redisConnectionString, options =>
{
    options.Configuration.ChannelPrefix = "EnterpriseChat";
    options.Configuration.AbortOnConnectFail = false;
    // تم حذف السطر المسبب للخطأ من هنا
});

builder.Services.AddScoped<IMessageBroadcaster, SignalRMessageBroadcaster>();

// AuthN/AuthZ
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
                    context.Token = token;

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

    //var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    //var seeder = new DatabaseSeeder(db, hasher);
    //await seeder.SeedAsync();
}

// ✅ مهم جدًا
app.UseRouting();

app.UseCors("client");

app.UseAuthentication();
app.UseAuthorization();

// ✅ اربط CORS بالـ endpoints
app.MapControllers().RequireCors("client");
app.MapHub<ChatHub>("/hubs/chat").RequireCors("client");

app.Run();

static class SwaggerExt
{
    public static OpenApiSecurityScheme AsSecurityScheme(this OpenApiReference r) => new() { Reference = r };
}