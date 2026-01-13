using EnterpriseChat.Application.Features.Messaging.Handlers;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Events;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Infrastructure.Events;
using EnterpriseChat.Infrastructure.Messaging;
using EnterpriseChat.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseChat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageReceiptRepository, MessageReceiptRepository>();
        services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        services.AddScoped<IRoomAuthorizationService, RoomAuthorizationService>();

        services.AddScoped<IDomainEventHandler<MessageDeliveredEvent>,
            MessageDeliveredEventHandler>();

        services.AddScoped<IDomainEventHandler<MessageReadEvent>,
            MessageReadEventHandler>();
        services.AddScoped<IMessageReceiptReadRepository,
                  MessageReceiptReadRepository>();
        services.AddScoped<DeliverMessageCommandHandler>();
        services.AddScoped<ReadMessageCommandHandler>();
        services.AddScoped<BlockUserCommandHandler>();
        services.AddScoped<MuteRoomCommandHandler>();
        services.AddScoped<UnmuteRoomCommandHandler>();
        services.AddScoped<IUserBlockRepository, UserBlockRepository>();
        services.AddScoped<IMutedRoomRepository, MutedRoomRepository>();
        services.AddScoped<IDomainEventDispatcher, NoOpDomainEventDispatcher>();
        return services;
    }
}
