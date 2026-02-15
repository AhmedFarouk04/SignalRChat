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
        // ✅ لو الصفحة اتغيرت من chat لحاجة تانية، نحدث الحالة
        bool wasChatPage = _isChatPage;

        _currentPage = page;
        _isChatPage = page == "chat";

        if (roomId.HasValue)
            _currentRoomId = roomId.Value;
        else
            _currentRoomId = Guid.Empty; // ✅ لو مفيش roomId، نفضيها

        if (currentUserId.HasValue)
            _currentUserId = currentUserId.Value;

        Console.WriteLine($"[Notification] Page changed: {page}, isChatPage: {_isChatPage}, roomId: {_currentRoomId}");
    }

    public async Task<bool> ShouldPlaySound(MessageModel message)
    {
        try
        {
            // 1. التأكد من وجود رسالة
            if (message == null)
                return false;

            Console.WriteLine($"[Notification] Checking message {message.Id} in room {message.RoomId}");
            Console.WriteLine($"[Notification] Current page: {_currentPage}, isChatPage: {_isChatPage}, currentRoom: {_currentRoomId}");

            // 2. مش في صفحة الشات - يشتغل
            if (!_isChatPage)
            {
                Console.WriteLine($"[Notification] NOT in chat page, PLAYING sound");
                return true;
            }

            // 3. في صفحة الشات ونفس الغرفة - ما يشتغلش
            if (_isChatPage && _currentRoomId == message.RoomId && _currentRoomId != Guid.Empty)
            {
                Console.WriteLine($"[Notification] In chat page and same room, NOT playing sound");
                return false;
            }

            // 4. في صفحة الشات لكن غرفة مختلفة - يشتغل
            if (_isChatPage && _currentRoomId != message.RoomId)
            {
                Console.WriteLine($"[Notification] In chat page but different room, PLAYING sound");
                return true;
            }

            // 5. رسالة من نفسي
            if (_currentUserId != Guid.Empty && message.SenderId == _currentUserId)
            {
                Console.WriteLine($"[Notification] My own message, NOT playing sound");
                return false;
            }

            // 6. الغرفة مكتومة
            if (_flagsStore.GetMuted(message.RoomId))
            {
                Console.WriteLine($"[Notification] Error: {{ex.Message}}");
                return false;
            }

            // 7. المستخدم محظور
            if (_flagsStore.GetBlockedByMe(message.SenderId) ||
                _flagsStore.GetBlockedMe(message.SenderId))
            {
                Console.WriteLine($"[Notification] User is blocked, NOT playing sound");
                return false;
            }

            // 8. افتراضياً نشتغل
            Console.WriteLine($"[Notification] Default: playing sound");
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