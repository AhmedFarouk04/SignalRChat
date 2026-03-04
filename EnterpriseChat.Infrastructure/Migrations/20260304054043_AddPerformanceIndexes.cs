using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─────────────────────────────────────────────────────────────
            // Messages: أهم index في التطبيق
            // يُستخدم في: GetMessages, GetUnreadCount, GetLastMessages
            // بيغطي الـ query: WHERE RoomId = X ORDER BY CreatedAt DESC
            // ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Messages_RoomId_CreatedAt'
                    AND object_id = OBJECT_ID('Messages')
                )
                CREATE INDEX IX_Messages_RoomId_CreatedAt
                    ON Messages (RoomId, CreatedAt DESC)
                    INCLUDE (SenderId, Content, IsBlocked, IsDeleted, IsSystemMessage, SystemMessageType);
            ");

            // ─────────────────────────────────────────────────────────────
            // Messages: للـ unread count query
            // WHERE RoomId = X AND SenderId <> Y AND IsDeleted = 0
            // ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Messages_RoomId_SenderId_IsDeleted'
                    AND object_id = OBJECT_ID('Messages')
                )
                CREATE INDEX IX_Messages_RoomId_SenderId_IsDeleted
                    ON Messages (RoomId, SenderId, IsDeleted, IsBlocked)
                    INCLUDE (CreatedAt, IsSystemMessage);
            ");

            // ─────────────────────────────────────────────────────────────
            // MessageReceipts: للـ unread bulk query
            // WHERE UserId = X AND Status < Read AND RoomId = Y
            // موجود في الـ snapshot بس بدون INCLUDE — هنحسنه
            // ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_MessageReceipts_UserId_Status_RoomId_Covering'
                    AND object_id = OBJECT_ID('MessageReceipts')
                )
                CREATE INDEX IX_MessageReceipts_UserId_Status_RoomId_Covering
                    ON MessageReceipts (UserId, Status, RoomId)
                    INCLUDE (MessageId, UpdatedAt);
            ");

            // ─────────────────────────────────────────────────────────────
            // MessageReceipts: lookup by MessageId (split query)
            // ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_MessageReceipts_MessageId_Covering'
                    AND object_id = OBJECT_ID('MessageReceipts')
                )
                CREATE INDEX IX_MessageReceipts_MessageId_Covering
                    ON MessageReceipts (MessageId)
                    INCLUDE (UserId, Status, RoomId);
            ");

            // ─────────────────────────────────────────────────────────────
            // ChatRoomMembers: lookup by UserId (GetForUserAsync)
            // موجود بالفعل لكن بدون INCLUDE
            // ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_ChatRoomMembers_UserId_Covering'
                    AND object_id = OBJECT_ID('ChatRoomMembers')
                )
                CREATE INDEX IX_ChatRoomMembers_UserId_Covering
                    ON ChatRoomMembers (UserId)
                    INCLUDE (RoomId, LastReadMessageId, LastReadAt, IsAdmin, IsOwner);
            ");

            // ─────────────────────────────────────────────────────────────
            // MessageDeletions: JOIN مع Messages (GetMessagesAsync)
            // WHERE UserId = X → يجيب MessageIds
            // ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_MessageDeletions_UserId_MessageId'
                    AND object_id = OBJECT_ID('MessageDeletions')
                )
                CREATE INDEX IX_MessageDeletions_UserId_MessageId
                    ON MessageDeletions (UserId)
                    INCLUDE (MessageId);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Messages_RoomId_CreatedAt ON Messages;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Messages_RoomId_SenderId_IsDeleted ON Messages;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_MessageReceipts_UserId_Status_RoomId_Covering ON MessageReceipts;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_MessageReceipts_MessageId_Covering ON MessageReceipts;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_ChatRoomMembers_UserId_Covering ON ChatRoomMembers;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_MessageDeletions_UserId_MessageId ON MessageDeletions;");
        }
    }
}