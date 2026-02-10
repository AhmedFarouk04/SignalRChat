using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EnterpriseChat.Infrastructure.Persistence;

public sealed class ChatDbContext : DbContext
{
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
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

        modelBuilder.Entity<ChatRoomMember>(entity =>
        {
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

        modelBuilder.Entity<MessageReceipt>(entity =>
        {
            entity.Property(e => e.MessageId)
                .HasConversion(
                    v => v.Value,
                    v => new MessageId(v))
                .Metadata.SetValueComparer(messageIdComparer);

            entity.Property(e => e.UserId)
                .HasConversion(
                    v => v.Value,
                    v => new UserId(v))
                .Metadata.SetValueComparer(userIdComparer);

            modelBuilder.Entity<Message>()
        .HasIndex(m => m.Content)
        .HasDatabaseName("IX_Message_Content");
        });

        // ✅ أضف باقي Entities حسب الحاجة
    }
}