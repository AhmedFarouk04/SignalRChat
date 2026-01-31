using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Events;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Infrastructure.Messaging;
using EnterpriseChat.Infrastructure.Persistence;
using EnterpriseChat.Infrastructure.Presence;
using EnterpriseChat.Infrastructure.Repositories;
using EnterpriseChat.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EnterpriseChat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPresenceService, RedisPresenceService>();
        services.AddScoped<IRoomPresenceService, RedisRoomPresenceService>();
        services.AddScoped<ITypingService, RedisTypingService>();

        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageReceiptRepository, MessageReceiptRepository>();
        services.AddScoped<IMessageReadRepository, MessageReadRepository>();
        services.AddScoped<IMessageReceiptReadRepository, MessageReceiptReadRepository>();

        services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        services.AddScoped<IUserBlockRepository, UserBlockRepository>();
        services.AddScoped<IMutedRoomRepository, MutedRoomRepository>();

        services.AddScoped<IRoomAuthorizationService, RoomAuthorizationService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<MessageDeliveredEvent>, MessageDeliveredEventHandler>();
        services.AddScoped<IDomainEventHandler<MessageReadEvent>, MessageReadEventHandler>();

        services.AddScoped<IUserDirectoryService, UserDirectoryService>();

        services.AddScoped<IAttachmentService, LocalAttachmentService>();

        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<IRoomDetailsReader, RoomDetailsReader>();

        return services;
    }


}
