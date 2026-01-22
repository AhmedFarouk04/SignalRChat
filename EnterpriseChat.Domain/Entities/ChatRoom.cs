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



    public void AddMember(UserId userId, bool isOwner = false)
    {
        if (_members.Any(m => m.UserId.Value == userId.Value))
            return;

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
            throw new InvalidOperationException(
                "Cannot remove members from private rooms.");

        var member = _members.FirstOrDefault(m => m.UserId.Value == userId.Value);
        if (member != null)
            _members.Remove(member);
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
    => _members.Select(m => m.UserId).ToList();
    public bool IsMember(UserId userId)
      => _members.Any(m => m.UserId.Value == userId.Value);


    public bool IsPrivateWith(UserId a, UserId b)
     => Type == RoomType.Private &&
        IsMember(a) &&
        IsMember(b);

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




}
