using EnterpriseChat.API.Messaging;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Events;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Infrastructure.Messaging;
using EnterpriseChat.Infrastructure.Persistence;
using EnterpriseChat.Infrastructure.Persistence.Seeding;
using EnterpriseChat.Infrastructure.Presence;
using EnterpriseChat.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace EnterpriseChat.API.Extensions;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChatServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext
        services.AddDbContext<ChatDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        services.AddScoped<IMessageReadRepository, MessageReadRepository>();
        services.AddScoped<IMutedRoomRepository, MutedRoomRepository>();
        services.AddScoped<IUserBlockRepository, UserBlockRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Domain Events
        services.AddScoped<IDomainEventHandler<MessageDeliveredEvent>,
            MessageDeliveredEventHandler>();
        services.AddScoped<IDomainEventHandler<MessageReadEvent>,
            MessageReadEventHandler>();

        // SignalR broadcaster
        services.AddScoped<IMessageBroadcaster, SignalRMessageBroadcaster>();

        // Authorization / Presence
        services.AddScoped<IRoomAuthorizationService, RoomAuthorizationService>();
        services.AddScoped<IRoomPresenceService, RedisRoomPresenceService>();
        services.AddScoped<ITypingService, RedisTypingService>();

        // Presence provider
        var provider = configuration["Presence:Provider"];
        if (provider == "Redis")
        {
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(
                    configuration["Presence:Redis"]!));

            services.AddSingleton<IPresenceService, RedisPresenceService>();
        }
        else
        {
            services.AddSingleton<IPresenceService, InMemoryPresenceService>();
        }

        // Seeder (dev only)
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
