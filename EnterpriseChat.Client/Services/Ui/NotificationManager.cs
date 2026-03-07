using Microsoft.JSInterop;
using EnterpriseChat.Client.Services.Ui;
using EnterpriseChat.Client.Models;

public class NotificationManager
{
    private readonly NotificationSoundService _soundService;
    private readonly RoomFlagsStore _flagsStore;

    private string _currentPage = "";
    private Guid _currentRoomId = Guid.Empty;
    private bool _isChatPage = false;
    private Guid _currentUserId = Guid.Empty;

    public NotificationManager(
        NotificationSoundService soundService,
        RoomFlagsStore flagsStore)
    {
        _soundService = soundService;
        _flagsStore = flagsStore;
    }

    public void SetCurrentPage(string page, Guid? roomId = null, Guid? currentUserId = null)
    {
                bool wasChatPage = _isChatPage;

        _currentPage = page;
        _isChatPage = page == "chat";

        if (roomId.HasValue)
            _currentRoomId = roomId.Value;
        else
            _currentRoomId = Guid.Empty; 
        if (currentUserId.HasValue)
            _currentUserId = currentUserId.Value;

        Console.WriteLine($"[Notification] Page changed: {page}, isChatPage: {_isChatPage}, roomId: {_currentRoomId}");
    }

    public async Task<bool> ShouldPlaySound(MessageModel message)
    {
        try
        {
            if (message == null) return false;

            Console.WriteLine($"[Notification] Checking message {message.Id} in room {message.RoomId}");

                        if (_currentUserId != Guid.Empty && message.SenderId == _currentUserId)
            {
                Console.WriteLine($"[Notification] My own message, NOT playing sound");
                return false;
            }

                        if (_isChatPage && _currentRoomId == message.RoomId && _currentRoomId != Guid.Empty)
            {
                Console.WriteLine($"[Notification] In same chat room, NOT playing sound");
                return false;
            }

                        if (_flagsStore.GetBlockedByMe(message.SenderId) || _flagsStore.GetBlockedMe(message.SenderId))
            {
                Console.WriteLine($"[Notification] User is blocked, NOT playing sound");
                return false;
            }

                                    bool isMuted = _flagsStore.GetMuted(message.RoomId);

            if (isMuted)
            {
                Console.WriteLine($"[Notification] Room is MUTED (from cache), NOT playing sound");
                return false;
            }

                        Console.WriteLine($"[Notification] PLAYING sound");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Notification] Error in ShouldPlaySound: {ex.Message}");
            return false;
        }
    }
    public async Task TryPlayNotification(MessageModel message)
    {
        try
        {
            if (await ShouldPlaySound(message))
            {
                await _soundService.PlayAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Notification] Error playing sound: {ex.Message}");
        }
    }
}