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
    public DbSet<PinnedMessage> PinnedMessages => Set<PinnedMessage>();
    public DbSet<MessageDeletion> MessageDeletions => Set<MessageDeletion>();

    public ChatDbContext(DbContextOptions<ChatDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

                ConfigureValueObjects(modelBuilder);

                modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
    }

    private void ConfigureValueObjects(ModelBuilder modelBuilder)
    {
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
                .HasColumnName("UserId")
                .Metadata.SetValueComparer(userIdComparer);

                        entity.Property<Guid?>("ChatUserId")
                .HasColumnName("ChatUserId")
                .IsRequired(false);  
                        entity.Property(e => e.LastReadMessageId)
                .HasConversion(
                    v => v != null ? v.Value : (Guid?)null,
                    v => v.HasValue ? new MessageId(v.Value) : null)
                .IsRequired(false)
                .Metadata.SetValueComparer(messageIdComparer);

            entity.Property(e => e.LastReadAt)
                .IsRequired(false);
                        entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(e => e.DeletedAt)
                .IsRequired(false);

            entity.Property(e => e.ClearedAt)
                .IsRequired(false);
                        entity.HasOne<ChatRoom>()
                .WithMany(r => r.Members)
                .HasForeignKey(e => e.RoomId)
                .HasPrincipalKey(r => r.Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ChatUser>()
                .WithMany()
                .HasForeignKey("ChatUserId")
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.SetNull);          });

                modelBuilder.Entity<MessageReceipt>(entity =>
        {
            entity.Property(e => e.MessageId)
                .HasConversion(
                    v => v.Value,
                    v => new MessageId(v))
                .Metadata.SetValueComparer(messageIdComparer);

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

                modelBuilder.Entity<Message>()
            .HasIndex(m => m.Content)
            .HasDatabaseName("IX_Message_Content");


                modelBuilder.Entity<Reaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(
                    v => v.Value,
                    v => new ReactionId(v)); 
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

            entity.Property(e => e.Type)
                .HasConversion<int>();

            entity.HasOne<Message>()
                .WithMany(m => m.Reactions)
                .HasForeignKey(r => r.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => new { r.MessageId, r.UserId })
                .IsUnique();
        });

                modelBuilder.Entity<PinnedMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RoomId)
                .HasConversion(
                    v => v.Value,
                    v => new RoomId(v))
                .Metadata.SetValueComparer(roomIdComparer);

            entity.Property(e => e.MessageId)
                .HasConversion(
                    v => v.Value,
                    v => new MessageId(v))
                .Metadata.SetValueComparer(messageIdComparer);

            entity.Property(e => e.PinnedByUserId)
                .HasConversion(
                    v => v.Value,
                    v => new UserId(v))
                .Metadata.SetValueComparer(userIdComparer);

            

            entity.HasOne<Message>()
                .WithMany()
                .HasForeignKey(e => e.MessageId)
                .HasPrincipalKey(m => m.Id)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.RoomId, e.MessageId }).IsUnique();
            entity.HasIndex(e => e.PinnedAt);
        });


        modelBuilder.Entity<MessageDeletion>(entity =>
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
        });
    }
}