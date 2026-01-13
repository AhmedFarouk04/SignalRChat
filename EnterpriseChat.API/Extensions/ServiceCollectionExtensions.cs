using EnterpriseChat.API.Messaging;
using EnterpriseChat.Application.Features.Messaging.Handlers;
using EnterpriseChat.Application.Features.Messaging.Queries;
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

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

		// Domain Events
		services.AddScoped<IDomainEventHandler<MessageDeliveredEvent>,
	     MessageDeliveredEventHandler>();

		services.AddScoped<IDomainEventHandler<MessageReadEvent>,
			MessageReadEventHandler>();
		// SignalR Broadcaster (API implementation)
		services.AddScoped<IMessageBroadcaster, SignalRMessageBroadcaster>();
		// Commands
		services.AddScoped<SendMessageCommandHandler>();

        // Repositories
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Seeder
        services.AddScoped<DatabaseSeeder>();

        services.AddScoped<IMessageReadRepository, MessageReadRepository>();
        services.AddScoped<GetMessagesQueryHandler>();



        services.AddScoped<IRoomAuthorizationService, RoomAuthorizationService>();


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
        return services;
    }
}
