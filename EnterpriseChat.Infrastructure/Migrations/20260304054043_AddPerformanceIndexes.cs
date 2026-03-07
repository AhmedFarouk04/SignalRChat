using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
        public partial class AddPerformanceIndexes : Migration
    {
                protected override void Up(MigrationBuilder migrationBuilder)
        {
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