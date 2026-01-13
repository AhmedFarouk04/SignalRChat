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

    // Group owner (for Group rooms only)
    public UserId? OwnerId { get; private set; }



    private readonly List<ChatRoomMember> _members = new();
    public IReadOnlyCollection<ChatRoomMember> Members => _members.AsReadOnly();
    private ChatRoom() { }

    // ✅ Keep your existing ctor (useful for creating rooms manually)
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

        AddMember(creatorId);
    }

    // 🔥 Factory: Private room auto-create
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

    // 🔥 Factory: Group room
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
            CreatedAt = DateTime.UtcNow
        };

        room.AddMember(creator);

        foreach (var member in members.Distinct())
        {
            if (member != creator)
                room.AddMember(member);
        }


        return room;
    }

    // ✅ Your existing seeding helper (keep it)
    public static ChatRoom Seed(
        RoomId id,
        string name,
        RoomType type,
        IEnumerable<UserId> members)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Room name is required.");

        var room = new ChatRoom
        {
            Id = id,
            Name = name,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var member in members)
            room.AddMember(member);

        return room;
    }

    public void AddMember(UserId userId, bool isOwner = false)
    {
        if (_members.Any(m => m.UserId == userId))
            return;

        _members.Add(ChatRoomMember.Create(Id, userId, isOwner));
    }

    public void RemoveMember(UserId userId)
    {
        if (Type == RoomType.Private)
            throw new InvalidOperationException(
                "Cannot remove members from private rooms.");

        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member != null)
            _members.Remove(member);
    }


    public IReadOnlyList<UserId> GetMemberIds()
    => _members.Select(m => m.UserId).ToList();
    public bool IsMember(UserId userId)
    => _members.Any(m => m.UserId == userId);

    // 🔥 Helper for repository filtering
    public bool IsPrivateWith(UserId a, UserId b)
     => Type == RoomType.Private &&
        IsMember(a) &&
        IsMember(b);


    

}
