namespace EnterpriseChat.Client.Services.Http;

public static class ApiEndpoints
{
    public const string Rooms = "api/rooms";
    public static string Room(Guid roomId) => $"api/rooms/{roomId}";
    public static string RoomMessages(Guid roomId, int skip, int take)
        => $"api/chat/rooms/{roomId}/messages?skip={skip}&take={take}";

    public const string SendMessage = "api/chat/messages";
    public static string MessageReaders(Guid messageId) => $"api/chat/messages/{messageId}/readers";

    public static string GroupMembers(Guid roomId) => $"api/groups/{roomId}/members";
    public static string RemoveGroupMember(Guid roomId, Guid userId) => $"api/groups/{roomId}/members/{userId}";

    public static string Mute(Guid roomId) => $"api/chat/mute/{roomId}";
    public static string Block(Guid userId) => $"api/chat/block/{userId}";

    public static string OnlineUsersInRoom(Guid roomId) => $"api/chat/rooms/{roomId}/online-users";
}
