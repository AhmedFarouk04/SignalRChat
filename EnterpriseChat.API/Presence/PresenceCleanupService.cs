using EnterpriseChat.Application.Interfaces;

namespace EnterpriseChat.API.Presence
{
    public class PresenceCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<PresenceCleanupService> _logger;

        public PresenceCleanupService(IServiceProvider services, ILogger<PresenceCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

                    if (presenceService is RedisPresenceService redisPresence)
                    {
                        await redisPresence.CleanupStaleConnectionsAsync();
                    }

                    // ✅ زود الوقت لـ 30 ثانية بدل 10
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning up stale connections");
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
            }
        }

    }
}