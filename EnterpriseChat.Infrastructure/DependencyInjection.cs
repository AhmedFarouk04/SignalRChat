using EnterpriseChat.Application.Features.Messaging.Handlers;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Events;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Infrastructure.Events;
using EnterpriseChat.Infrastructure.Messaging;
using EnterpriseChat.Infrastructure.Presence;
using EnterpriseChat.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EnterpriseChat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ===============================
        // Redis
        // ===============================
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("Redis")!));

        // ===============================
        // Presence & Typing
        // ===============================
        services.AddScoped<IPresenceService, RedisPresenceService>();
        services.AddScoped<IRoomPresenceService, RedisRoomPresenceService>();
        services.AddScoped<ITypingService, RedisTypingService>();

        // ===============================
        // Repositories
        // ===============================
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageReceiptRepository, MessageReceiptRepository>();
        services.AddScoped<IMessageReceiptReadRepository, MessageReceiptReadRepository>();
        services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        services.AddScoped<IUserBlockRepository, UserBlockRepository>();
        services.AddScoped<IMutedRoomRepository, MutedRoomRepository>();
        services.AddScoped<IRoomAuthorizationService, RoomAuthorizationService>();

        // ===============================
        // Command Handlers
        // ===============================
        services.AddScoped<DeliverMessageCommandHandler>();
        services.AddScoped<ReadMessageCommandHandler>();
        services.AddScoped<BlockUserCommandHandler>();
        services.AddScoped<MuteRoomCommandHandler>();
        services.AddScoped<UnmuteRoomCommandHandler>();

        // ===============================
        // Domain Events
        // ===============================
        services.AddScoped<IDomainEventHandler<MessageDeliveredEvent>,
            MessageDeliveredEventHandler>();

        services.AddScoped<IDomainEventHandler<MessageReadEvent>,
            MessageReadEventHandler>();

        services.AddScoped<IDomainEventDispatcher, NoOpDomainEventDispatcher>();

        return services;
    }
}
