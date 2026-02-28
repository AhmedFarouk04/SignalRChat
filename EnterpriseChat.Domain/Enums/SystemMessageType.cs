namespace EnterpriseChat.Domain.Enums;

public enum SystemMessageType
{
    UserJoined,          // "X joined the room"
    UserLeft,            // "X left the room"
    MemberAdded,         // "You were added by Y"  ← personal
    MemberRemoved,       // "You were removed by Y" ← personal
    GroupRenamed,
    GroupCreated,
    MemberPromoted,
    MemberDemoted,
    
}