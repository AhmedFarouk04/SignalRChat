using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using System.Linq;

namespace EnterpriseChat.Domain.Entities;

public sealed class ChatRoom
{
    public RoomId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public RoomType Type { get; private set; }
    public DateTime CreatedAt { get; private set; }

        public UserId? OwnerId { get; private set; }
    public MessageId? PinnedMessageId { get; private set; }
    public DateTime? PinnedUntilUtc { get; private set; }

        public MessageId? LastMessageId { get; private set; }
    public string? LastMessagePreview { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public UserId? LastMessageSenderId { get; private set; }

    private readonly List<ChatRoomMember> _members = new();
    public string? LastReactionPreview { get; private set; }
    public DateTime? LastReactionAt { get; private set; }
    public UserId? LastReactionTargetUserId { get; private set; }
    public IReadOnlyCollection<ChatRoomMember> Members => _members.AsReadOnly();

    public IEnumerable<ChatRoomMember> ActiveMembers => _members.Where(m => !m.IsRemovedFromGroup);
    private readonly List<PinnedMessage> _pinnedMessages = new();
    public IReadOnlyCollection<PinnedMessage> PinnedMessages => _pinnedMessages.AsReadOnly();


    private ChatRoom() { }

    public ChatRoom(string name, RoomType type, UserId creatorId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Room name is required.");

        Id = RoomId.New();
        Name = name;
        Type = type;
        CreatedAt = DateTime.UtcNow;

        if (type == RoomType.Group)
            OwnerId = creatorId;

        AddMember(creatorId, isOwner: type == RoomType.Group);
    }
    public void SetLastReactionPreview(string preview, DateTime at, UserId targetUserId)
    {
        LastReactionPreview = preview;
        LastReactionAt = at;
        LastReactionTargetUserId = targetUserId;
    }
    public static ChatRoom CreatePrivate(UserId a, UserId b)
    {
        var room = new ChatRoom
        {
            Id = RoomId.New(),
            Name = "Private",
            Type = RoomType.Private,
            CreatedAt = DateTime.UtcNow,
            OwnerId = null
        };

        room.AddMember(a);
        room.AddMember(b);
        return room;
    }
    public void ClearLastReactionPreview()
    {
        LastReactionPreview = null;
        LastReactionAt = null;
        LastReactionTargetUserId = null;
    }
    public void DeleteForMember(UserId userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new InvalidOperationException("User is not a member.");
        member.DeleteForMe();
    }

    public void RestoreMember(UserId userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        member?.Restore();
    }

    public void ClearChatForMember(UserId userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new InvalidOperationException("User is not a member.");
        member.ClearChat();
    }

    public void ClearChatForAll()
    {
        if (Type != RoomType.Group)
            throw new InvalidOperationException("Clear for all is only for groups.");
        foreach (var member in _members)
            member.ClearChat();
    }
    public static ChatRoom CreateGroup(
      string name,
      UserId creator,
      IEnumerable<UserId> members)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name is required.");

        var room = new ChatRoom
        {
            Id = RoomId.New(),
            Name = name,
            Type = RoomType.Group,
            CreatedAt = DateTime.UtcNow,
            OwnerId = creator
        };

        room.AddMember(creator, isOwner: true);

        foreach (var member in members.Distinct())
        {
            if (member != creator)
                room.AddMember(member);
        }

        return room;
    }

        public void UpdateLastMessage(Message message)
    {
        if (message == null) return;

        LastMessageId = message.Id;
        LastMessagePreview = message.Content?.Length > 60
            ? message.Content.Substring(0, 60) + "..."
            : message.Content;
        LastMessageAt = message.CreatedAt;
        LastMessageSenderId = message.SenderId;
    }

    public void AddMember(UserId userId, bool isOwner = false)
    {
        var existingMember = _members.FirstOrDefault(m => m.UserId.Value == userId.Value);

        if (existingMember != null)
        {
            if (existingMember.IsRemovedFromGroup)
            {
                existingMember.RejoinGroup(isOwner, isOwner);
            }
            return;
        }

        var isAdmin = isOwner;
        _members.Add(ChatRoomMember.Create(Id, userId, isOwner, isAdmin));
    }
    public void Leave(UserId userId)
    {
        if (Type == RoomType.Private)
            throw new InvalidOperationException("Cannot leave private rooms.");

        if (OwnerId == userId)
            throw new InvalidOperationException("Owner cannot leave the group. Transfer ownership first.");

        if (!_members.Any(m => m.UserId == userId))
            throw new InvalidOperationException("User is not a member of this room.");

        if (_members.Count == 1)
            throw new InvalidOperationException("Cannot remove the last member of the group.");

        RemoveMember(userId);
    }

    public void RemoveMember(UserId userId)
    {
        if (Type == RoomType.Private)
            throw new InvalidOperationException("Cannot remove members from private rooms.");

        var member = _members.FirstOrDefault(m => m.UserId.Value == userId.Value);
        if (member != null)
        {
            member.MarkAsRemovedFromGroup();
        }
    }

    public void SetOwner(UserId newOwnerId)
    {
        if (Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms have owners.");

        OwnerId = newOwnerId;

        foreach (var m in _members)
        {
            if (m.UserId.Value == newOwnerId.Value)
                m.SetOwner(true);
            else
                m.SetOwner(false);
        }
    }

    public IReadOnlyList<UserId> GetMemberIds()
        => _members.Where(m => !m.IsRemovedFromGroup).Select(m => m.UserId).ToList();

    public bool IsMember(UserId userId)
        => _members.Any(m => m.UserId.Value == userId.Value);
    public IReadOnlyList<UserId> GetAllMemberIdsIncludingRemoved()
        => _members.Select(m => m.UserId).ToList();
    public bool IsPrivateWith(UserId a, UserId b)
        => Type == RoomType.Private &&
           IsMember(a) &&
           IsMember(b);
    public bool IsActiveMember(UserId userId)
        => _members.Any(m => m.UserId.Value == userId.Value && !m.IsRemovedFromGroup);
    public void Rename(string name)
    {
        if (Type != RoomType.Group)
            throw new InvalidOperationException("Only group rooms can be renamed.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name is required.");

        if (name.Length > 100)
            throw new ArgumentException("Group name is too long.");

        Name = name.Trim();
    }

    public void PinMessage(Guid? messageId, TimeSpan? duration, UserId pinnedBy)
    {
        if (messageId == null)
        {
            _pinnedMessages.Clear();
            PinnedMessageId = null;
            PinnedUntilUtc = null;
            return;
        }

        if (_pinnedMessages.Any(p => p.MessageId.Value == messageId))
            return;

        if (_pinnedMessages.Count >= 3)
        {
            var oldest = _pinnedMessages.OrderBy(p => p.PinnedAt).First();
            _pinnedMessages.Remove(oldest);
        }

        var pinned = PinnedMessage.Create(
            Id,
            new MessageId(messageId.Value),
            pinnedBy,
            duration);

        _pinnedMessages.Add(pinned);

                PinnedMessageId = new MessageId(messageId.Value);
        PinnedUntilUtc = pinned.PinnedUntilUtc;
    }
    public void UnpinMessage(Guid messageId)
    {
        var pin = _pinnedMessages.FirstOrDefault(p => p.MessageId.Value == messageId);
        if (pin != null)
            _pinnedMessages.Remove(pin);

        var last = _pinnedMessages.OrderByDescending(p => p.PinnedAt).FirstOrDefault();

                PinnedMessageId = last?.MessageId;
        PinnedUntilUtc = last?.PinnedUntilUtc;
    }
    public void ClearLastMessage()
    {
        LastMessageId = null;
        LastMessagePreview = null;
        LastMessageAt = null;
        LastMessageSenderId = null;
    }
    public void UnpinAll()
    {
        _pinnedMessages.Clear();
        PinnedMessageId = null;
        PinnedUntilUtc = null;
    }
}