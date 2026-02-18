using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EnterpriseChat.Infrastructure.Persistence;

public sealed class ChatDbContext : DbContext
{
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomMember> ChatRoomMembers => Set<ChatRoomMember>();
    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();
    public DbSet<MutedRoom> MutedRooms => Set<MutedRoom>();
    public DbSet<MessageReceipt> MessageReceipts => Set<MessageReceipt>();
    public DbSet<ChatUser> Users => Set<ChatUser>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Reaction> Reactions => Set<Reaction>();

    public ChatDbContext(DbContextOptions<ChatDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ أضف Value Converters هنا
        ConfigureValueObjects(modelBuilder);

        // ✅ ثم طبق Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
    }

    private void ConfigureValueObjects(ModelBuilder modelBuilder)
    {
        // ✅ Value Comparers للـ Value Objects
        var messageIdComparer = new ValueComparer<MessageId>(
            (l, r) => l!.Value == r!.Value,
            v => v.Value.GetHashCode(),
            v => new MessageId(v.Value));

        var roomIdComparer = new ValueComparer<RoomId>(
            (l, r) => l!.Value == r!.Value,
            v => v.Value.GetHashCode(),
            v => new RoomId(v.Value));

        var userIdComparer = new ValueComparer<UserId>(
            (l, r) => l!.Value == r!.Value,
            v => v.Value.GetHashCode(),
            v => new UserId(v.Value));

        // ✅ تكوين Entities مع Value Converters
        modelBuilder.Entity<Message>(entity =>
        {
            entity.Property(e => e.Id)
                .HasConversion(
                    v => v.Value,
                    v => new MessageId(v))
                .Metadata.SetValueComparer(messageIdComparer);

            entity.Property(e => e.RoomId)
                .HasConversion(
                    v => v.Value,
                    v => new RoomId(v))
                .Metadata.SetValueComparer(roomIdComparer);

            entity.Property(e => e.SenderId)
                .HasConversion(
                    v => v.Value,
                    v => new UserId(v))
                .Metadata.SetValueComparer(userIdComparer);

            entity.Property(e => e.ReplyToMessageId)
                .HasConversion(
                    v => v!.Value,
                    v => new MessageId(v))
                .Metadata.SetValueComparer(messageIdComparer);
        });

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.Property(e => e.Id)
                .HasConversion(
                    v => v.Value,
                    v => new RoomId(v))
                .Metadata.SetValueComparer(roomIdComparer);

            entity.Property(e => e.OwnerId)
                .HasConversion(
                    v => v!.Value,
                    v => new UserId(v))
                .Metadata.SetValueComparer(userIdComparer);
        });

        // ✅ ChatRoomMember - التعديل النهائي مع Shadow Property Nullable
        modelBuilder.Entity<ChatRoomMember>(entity =>
        {
            entity.Property(e => e.RoomId)
                .HasConversion(
                    v => v.Value,
                    v => new RoomId(v))
                .Metadata.SetValueComparer(roomIdComparer);

            // ✅ UserId Value Object - يقرأ من عامود UserId
            entity.Property(e => e.UserId)
                .HasConversion(
                    v => v.Value,
                    v => new UserId(v))
                .HasColumnName("UserId")
                .Metadata.SetValueComparer(userIdComparer);

            // ✅ Shadow property للـ FK - Nullable! (مش Required)
            entity.Property<Guid?>("ChatUserId")
                .HasColumnName("ChatUserId")
                .IsRequired(false);  // ✅ Nullable

            // ✅ LastReadMessageId
            entity.Property(e => e.LastReadMessageId)
                .HasConversion(
                    v => v != null ? v.Value : (Guid?)null,
                    v => v.HasValue ? new MessageId(v.Value) : null)
                .IsRequired(false)
                .Metadata.SetValueComparer(messageIdComparer);

            entity.Property(e => e.LastReadAt)
                .IsRequired(false);

            // ✅ العلاقات
            entity.HasOne<ChatRoom>()
                .WithMany(r => r.Members)
                .HasForeignKey(e => e.RoomId)
                .HasPrincipalKey(r => r.Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ChatUser>()
                .WithMany()
                .HasForeignKey("ChatUserId")
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.SetNull);  // ✅ SetNull مش Cascade
        });

        // في ConfigureValueObjects method
        modelBuilder.Entity<MessageReceipt>(entity =>
        {
            entity.Property(e => e.MessageId)
                .HasConversion(
                    v => v.Value,
                    v => new MessageId(v))
                .Metadata.SetValueComparer(messageIdComparer);

            // ✅ أضف هذا الجزء (RoomId)
            entity.Property(e => e.RoomId)
                .HasConversion(
                    v => v.Value,
                    v => new RoomId(v))
                .Metadata.SetValueComparer(roomIdComparer);

            entity.Property(e => e.UserId)
                .HasConversion(
                    v => v.Value,
                    v => new UserId(v))
                .Metadata.SetValueComparer(userIdComparer);
        });

        // ✅ Index للبحث في المحتوى
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.Content)
            .HasDatabaseName("IX_Message_Content");
    }
}