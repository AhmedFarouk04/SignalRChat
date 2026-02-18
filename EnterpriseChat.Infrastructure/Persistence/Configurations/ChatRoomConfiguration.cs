using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class ChatRoomConfiguration : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> builder)
    {
        builder.ToTable("ChatRooms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(
                v => v.Value,
                v => new RoomId(v))
            .Metadata.SetValueComparer(
                new ValueComparer<RoomId>(
                    (a, b) => a.Value == b.Value,
                    v => v.Value.GetHashCode(),
                    v => new RoomId(v.Value)));

        builder.Property(x => x.Id).IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200);

        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property(x => x.OwnerId)
            .HasConversion(
                v => v != null ? v.Value : (Guid?)null,
                v => v.HasValue ? new UserId(v.Value) : null)
            .IsRequired(false);

        builder.Property(x => x.PinnedMessageId).IsRequired(false);
        builder.Property(x => x.PinnedUntilUtc).IsRequired(false);

        // ✅ Last Message Fields (جديد)
        builder.Property(x => x.LastMessageId)
            .HasConversion(
                v => v != null ? v.Value : (Guid?)null,
                v => v.HasValue ? MessageId.From(v.Value) : null)
            .IsRequired(false);

        builder.Property(x => x.LastMessagePreview)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(x => x.LastMessageAt)
            .IsRequired(false);

        builder.Property(x => x.LastMessageSenderId)
            .HasConversion(
                v => v != null ? v.Value : (Guid?)null,
                v => v.HasValue ? new UserId(v.Value) : null)
            .IsRequired(false);

        // ✅ Members navigation
        builder.Metadata.FindNavigation(nameof(ChatRoom.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<Message>()
            .WithOne()
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
} 