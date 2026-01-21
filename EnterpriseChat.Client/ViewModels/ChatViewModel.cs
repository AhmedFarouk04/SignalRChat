using EnterpriseChat.Client.Authentication.Abstractions;
using EnterpriseChat.Client.Models;
using EnterpriseChat.Client.Services.Chat;
using EnterpriseChat.Client.Services.Realtime;
using EnterpriseChat.Client.Services.Rooms;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EnterpriseChat.Client.ViewModels;

public sealed class ChatViewModel
{
    private readonly IChatService _chatService;
    private readonly IRoomService _roomService;
    private readonly IChatRealtimeClient _realtime;
    private readonly ICurrentUser _currentUser;
    private DateTime _lastTyping;
    private Guid? _currentRoomId;
    public ChatViewModel(
        IChatService chatService,
        IRoomService roomService,
        IChatRealtimeClient realtime,
        ICurrentUser currentUser)
    {
        _chatService = chatService;
        _roomService = roomService;
        _realtime = realtime;
        _currentUser = currentUser;
    }

    public RoomModel? Room { get; private set; }
    public GroupMembersModel? GroupMembers { get; private set; }
    public UserModel? OtherUser { get; private set; }

    public List<MessageModel> Messages { get; } = new();
    public List<UserModel> OnlineUsers { get; } = new();
    public List<UserModel> TypingUsers { get; } = new();
    public string? UiError { get; private set; }
    public Guid CurrentUserId { get; private set; }

    public bool IsMuted { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool IsDisconnected { get; private set; }
    public bool IsOtherDeleted { get; private set; }
    public bool IsRemoved { get; private set; }
    public string? LastError { get; private set; }

    public async Task InitializeAsync(Guid roomId)
    {
        try
        {
            UiError = null;

            CurrentUserId = await _currentUser.GetUserIdAsync()
                ?? throw new InvalidOperationException("User not authenticated");

            Room = await _roomService.GetRoomAsync(roomId);
            if (Room == null)
                return;

            _currentRoomId = roomId;

            Messages.Clear();
            Messages.AddRange(await _chatService.GetMessagesAsync(roomId));

            if (Room.Type == "Group")
            {
                var dto = await _chatService.GetGroupMembersAsync(roomId);

                GroupMembers = new GroupMembersModel
                {
                    OwnerId = dto.OwnerId,
                    Members = dto.Members
                        .Select(m => new UserModel
                        {
                            Id = m.Id,
                            DisplayName = m.DisplayName
                        })
                        .ToList()
                };
            }

            if (Room.Type == "Private")
            {
                if (Room.OtherUserId == null)
                {
                    IsOtherDeleted = true;
                }
                else
                {
                    OtherUser = new UserModel
                    {
                        Id = Room.OtherUserId.Value,
                        DisplayName = Room.OtherDisplayName!,
                        IsOnline = false
                    };
                }
            }

            RegisterRealtimeEvents(roomId);

            await _realtime.ConnectAsync();
            await _realtime.JoinRoomAsync(roomId);
        }
        catch
        {
            UiError = "Failed to load chat. Please try again.";
            Room = null;
        }
    }


    private void RegisterRealtimeEvents(Guid roomId)
    {
        _realtime.MessageReceived += OnMessageReceived;
        _realtime.MessageDelivered += OnMessageDelivered;

        _realtime.MessageRead += id =>
        {
            var msg = Messages.FirstOrDefault(m => m.Id == id);
            if (msg != null && msg.Status != MessageStatus.Read)
                msg.Status = MessageStatus.Read;
        };

        _realtime.UserOnline += id =>
        {
            if (OtherUser?.Id == id)
            {
                OtherUser.IsOnline = true;
                OtherUser.LastSeen = null;
            }
        };

        _realtime.UserOffline += id =>
        {
            if (OtherUser?.Id == id)
            {
                OtherUser.IsOnline = false;
                OtherUser.LastSeen = DateTime.UtcNow;
            }
        };

        _realtime.RoomPresenceUpdated += async (rid, _) =>
        {
            if (rid != roomId)
                return;

            var users = await _chatService.GetOnlineUsersInRoomAsync(roomId);

            OnlineUsers.Clear();
            OnlineUsers.AddRange(
                users.Where(u => u.Id != CurrentUserId)
                     .Select(u => new UserModel
                     {
                         Id = u.Id,
                         DisplayName = u.DisplayName,
                         IsOnline = true
                     }));
        };

        _realtime.TypingStarted += (rid, uid) =>
        {
            if (rid != roomId || uid == CurrentUserId)
                return;

            if (TypingUsers.Any(u => u.Id == uid))
                return;

            var user = OnlineUsers.FirstOrDefault(u => u.Id == uid);
            if (user != null)
                TypingUsers.Add(user);
        };

        _realtime.TypingStopped += (_, uid) =>
        {
            TypingUsers.RemoveAll(u => u.Id == uid);
        };

        _realtime.Disconnected += () => IsDisconnected = true;
        _realtime.Reconnected += () => IsDisconnected = false;

        _realtime.RemovedFromRoom += rid =>
        {
            if (rid == roomId)
                IsRemoved = true;
        };
    }

    private void OnMessageReceived(MessageModel message)
    {
        var existing = Messages.FirstOrDefault(m =>
            m.Status == MessageStatus.Pending &&
            m.Content == message.Content &&
            m.SenderId == message.SenderId);

        if (existing != null)
        {
            existing.Id = message.Id;
            existing.Status = MessageStatus.Sent;
            existing.Error = null;
        }
        else
        {
            Messages.Add(message);
        }
    }

    private void OnMessageDelivered(Guid messageId)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg?.Status == MessageStatus.Sent)
            msg.Status = MessageStatus.Delivered;
    }


    private void ClearError()
    {
        UiError = null;
    }


    public async Task SendAsync(Guid roomId, string text)
    {
        ClearError();

        if (IsBlocked)
        {
            LastError = "You cannot send messages to this user.";
            return;
        }

        if (IsOtherDeleted)
        {
            LastError = "This user is no longer available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            LastError = "Message cannot be empty.";
            return;
        }

        if (text.Length > 2000)
        {
            LastError = "Message is too long.";
            return;
        }

        var pending = new MessageModel
        {
            Id = Guid.Empty,
            RoomId = roomId,
            SenderId = CurrentUserId,
            Content = text,
            CreatedAt = DateTime.UtcNow,
            Status = MessageStatus.Pending
        };

        Messages.Add(pending);

        try
        {
            var dto = await _chatService.SendMessageAsync(roomId, text);
            pending.Id = dto.Id;
            pending.Status = MessageStatus.Sent;
        }
        catch
        {
            pending.Status = MessageStatus.Failed;
            pending.Error = "Failed to send message.";
            LastError = "Network error. Please try again.";
        }
    }

    public async Task NotifyTypingAsync(Guid roomId)
    {
        if ((DateTime.UtcNow - _lastTyping).TotalMilliseconds < 800)
            return;

        _lastTyping = DateTime.UtcNow;
        await _realtime.NotifyTypingAsync(roomId);
    }

    public Task MarkRoomReadAsync(Guid roomId, Guid lastMessageId)
        => _realtime.MarkRoomReadAsync(roomId, lastMessageId);

    public async Task RemoveMemberAsync(Guid roomId, Guid userId)
    {
        await _chatService.RemoveMemberAsync(roomId, userId);
        GroupMembers?.Members.RemoveAll(u => u.Id == userId);
    }

    public async Task BlockUserAsync(Guid userId)
    {
        await _chatService.BlockUserAsync(userId);
        IsBlocked = true;
        TypingUsers.Clear();
    }

    public async Task ToggleMuteAsync(Guid roomId)
    {
        if (!IsMuted)
            await _chatService.MuteAsync(roomId);
        else
            await _chatService.UnmuteAsync(roomId);

        IsMuted = !IsMuted;
    }

    public async Task DisposeAsync()
    {
        if (_currentRoomId != null)
        {
            await _realtime.LeaveRoomAsync(_currentRoomId.Value);
        }

        await _realtime.DisconnectAsync();
    }



}
