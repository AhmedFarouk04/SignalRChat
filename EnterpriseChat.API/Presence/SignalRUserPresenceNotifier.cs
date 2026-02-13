namespace EnterpriseChat.API.Presence
{
    using EnterpriseChat.API.Hubs;
    using EnterpriseChat.Application.Interfaces;
    using Microsoft.AspNetCore.SignalR;

    public sealed class SignalRUserPresenceNotifier : IUserPresenceNotifier
    {
        private readonly IHubContext<ChatHub> _hub;

        public SignalRUserPresenceNotifier(IHubContext<ChatHub> hub)
        {
            _hub = hub;
        }

        public async Task HideUsersFromEachOtherAsync(Guid a, Guid b, CancellationToken ct = default)
        {
            await _hub.Clients.User(a.ToString()).SendAsync("UserOffline", b, ct);
            await _hub.Clients.User(b.ToString()).SendAsync("UserOffline", a, ct);
        }

        public async Task BlockChangedAsync(Guid blockerId, Guid blockedId, bool blocked, CancellationToken ct = default)
        {
            // اللي عمل البلوك
            await _hub.Clients.User(blockerId.ToString())
                .SendAsync("UserBlockedByMeChanged", blockedId, blocked, ct);

            // اللي اتعمله بلوك
            await _hub.Clients.User(blockedId.ToString())
                .SendAsync("UserBlockedMeChanged", blockerId, blocked, ct);

            // Presence hide (اختياري لكن مفيد)
            if (blocked)
                await HideUsersFromEachOtherAsync(blockerId, blockedId, ct);
        }

    }

}
