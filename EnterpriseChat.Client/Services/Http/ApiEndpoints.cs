namespace EnterpriseChat.Client.Services.Http;

public static class ApiEndpoints
{
        public const string Register = "api/auth/register";
    public const string VerifyEmail = "api/auth/verify-email";
    public const string ResendCode = "api/auth/resend-code";
    public const string Login = "api/auth/login";
    public const string Logout = "api/auth/logout";

        public const string Rooms = "api/rooms";
    public static string Room(Guid roomId) => $"api/rooms/{roomId}";

        public static string UserSearch(string query, int take = 20)
        => $"api/users/search?query={Uri.EscapeDataString(query)}&take={take}";

        public const string SendMessage = "api/chat/messages";
    public static string RoomMessages(Guid roomId, int skip, int take)
        => $"api/chat/rooms/{roomId}/messages?skip={skip}&take={take}";
    public static string MessageReaders(Guid messageId) => $"api/chat/messages/{messageId}/readers";

    public static string MarkMessageDelivered(Guid messageId) => $"api/chat/messages/{messageId}/delivered";
    public static string MarkMessageRead(Guid messageId) => $"api/chat/messages/{messageId}/read";

    public static string DeliverRoom(Guid roomId) => $"api/chat/rooms/{roomId}/delivered";
    public static string MarkRoomRead(Guid roomId) => $"api/chat/rooms/{roomId}/read";

        public static string GetOrCreatePrivate(Guid userId) => $"api/chat/private/{userId}";

        public static string Block(Guid userId) => $"api/chat/block/{userId}";
    public const string Blocked = "api/chat/blocked";
    public static string Unblock(Guid userId) => $"api/chat/block/{userId}";

    public static string Mute(Guid roomId) => $"api/chat/mute/{roomId}";
    public const string Muted = "api/chat/muted";
    public static string Unmute(Guid roomId) => $"api/chat/mute/{roomId}";

        public static string UploadAttachment(Guid roomId) => $"api/chat/rooms/{roomId}/attachments";
    public static string ListRoomAttachments(Guid roomId, int skip, int take)
        => $"api/chat/rooms/{roomId}/attachments?skip={skip}&take={take}";

        public static string DownloadAttachment(Guid attachmentId) => $"api/attachments/{attachmentId}";
    public static string DeleteAttachment(Guid attachmentId) => $"api/attachments/{attachmentId}";

        public const string Groups = "api/groups";
    public static string Group(Guid roomId) => $"api/groups/{roomId}";
    public static string GroupMembers(Guid roomId) => $"api/groups/{roomId}/members";
    public static string AddGroupMember(Guid roomId, Guid userId) => $"api/groups/{roomId}/members/{userId}";
    public static string RemoveGroupMember(Guid roomId, Guid userId) => $"api/groups/{roomId}/members/{userId}";
    public static string LeaveGroup(Guid roomId) => $"api/groups/{roomId}/leave";
    public static string GroupAdmins(Guid roomId) => $"api/groups/{roomId}/admins";
    public static string PromoteAdmin(Guid roomId, Guid userId) => $"api/groups/{roomId}/admins/{userId}";
    public static string DemoteAdmin(Guid roomId, Guid userId) => $"api/groups/{roomId}/admins/{userId}";
    public static string TransferOwner(Guid roomId, Guid userId) => $"api/groups/{roomId}/owner/{userId}";
    public static string UpdateGroup(Guid roomId) => $"api/groups/{roomId}";       public static string DeleteGroup(Guid roomId) => $"api/groups/{roomId}";       public static string GetGroup(Guid roomId) => $"api/groups/{roomId}";   
        public const string OnlineUsers = "api/presence/online";
    public static string IsOnline(Guid userId) => $"api/presence/online/{userId}";

        public static string TypingStart(Guid roomId) => $"api/rooms/{roomId}/typing/start";
    public static string TypingStop(Guid roomId) => $"api/rooms/{roomId}/typing/stop";
    public static string StartTyping(Guid roomId) => $"/api/rooms/{roomId}/typing/start";
    public static string StopTyping(Guid roomId) => $"/api/rooms/{roomId}/typing/stop";


    public static string EditMessage(Guid messageId) => $"api/chat/messages/{messageId}";
    public static string DeleteMessage(Guid messageId, bool forEveryone) =>
        $"api/chat/messages/{messageId}?deleteForEveryone={forEveryone}";

        public static string SearchMessages(Guid roomId, string query, int take)
        => $"api/chat/rooms/{roomId}/messages/search?query={Uri.EscapeDataString(query)}&take={take}";

    public static string UserSearch(string query, int take, Guid excludeUserId)          => $"api/users/search?query={Uri.EscapeDataString(query)}&take={take}&excludeUserId={excludeUserId}";

    public static string AddGroupMembersBulk(Guid roomId) => $"api/groups/{roomId}/members/bulk";
}
