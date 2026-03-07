using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Realtime;

namespace EnterpriseChat.Client.Services.Ui;

public sealed class GlobalNotificationHandler : IAsyncDisposable
{
    private readonly IChatRealtimeClient _realtime;
    private readonly NotificationManager _notificationManager;
    private bool _subscribed = false;

    public GlobalNotificationHandler(
        IChatRealtimeClient realtime,
        NotificationManager notificationManager)
    {
        _realtime = realtime;
        _notificationManager = notificationManager;
    }

    public void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        _realtime.MessageReceived += OnMessageReceived;
        Console.WriteLine("[GlobalNotification] ✅ Subscribed to MessageReceived");
    }

    public void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;
        _realtime.MessageReceived -= OnMessageReceived;
    }

    private async void OnMessageReceived(MessageModel message)
    {
        try
        {
            Console.WriteLine($"[GlobalNotification] Message received: {message.Id}");
            await _notificationManager.TryPlayNotification(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GlobalNotification] Error: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync()
    {
        Unsubscribe();
        return ValueTask.CompletedTask;
    }
}