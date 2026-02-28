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
        // 1. تعريف الـ Comparers الموحدة للـ Nullable Types
        var messageIdNullableComparer = new ValueComparer<MessageId?>(
            (l, r) => (l == null && r == null) || (l != null && r != null && l.Value == r.Value),
            v => v == null ? 0 : v.Value.GetHashCode(),
            v => v == null ? null : new MessageId(v.Value));

        var userIdNullableComparer = new ValueComparer<UserId?>(
            (l, r) => (l == null && r == null) || (l != null && r != null && l.Value == r.Value),
            v => v == null ? 0 : v.Value.GetHashCode(),
            v => v == null ? null : new UserId(v.Value));

        builder.ToTable("ChatRooms");

        // Primary Key
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(v => v.Value, v => new RoomId(v))
            .Metadata.SetValueComparer(new ValueComparer<RoomId>(
                (a, b) => a!.Value == b!.Value,
                v => v.Value.GetHashCode(),
                v => new RoomId(v.Value)));

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // ✅ PinnedMessageId - التعديل الأساسي
        builder.Property(x => x.PinnedMessageId)
            .HasConversion(
                v => v != null ? v.Value : (Guid?)null,
                v => v.HasValue ? new MessageId(v.Value) : null)
            .Metadata.SetValueComparer(messageIdNullableComparer);

        builder.Property(x => x.PinnedUntilUtc).IsRequired(false);

        // ✅ OwnerId
        builder.Property(x => x.OwnerId)
            .HasConversion(v => v != null ? v.Value : (Guid?)null, v => v.HasValue ? new UserId(v.Value) : null)
            .Metadata.SetValueComparer(userIdNullableComparer);

        // ✅ Last Message Fields
        builder.Property(x => x.LastMessageId)
            .HasConversion(v => v != null ? v.Value : (Guid?)null, v => v.HasValue ? MessageId.From(v.Value) : null)
            .Metadata.SetValueComparer(messageIdNullableComparer);

        builder.Property(x => x.LastMessageSenderId)
            .HasConversion(v => v != null ? v.Value : (Guid?)null, v => v.HasValue ? new UserId(v.Value) : null)
            .Metadata.SetValueComparer(userIdNullableComparer);

        // ✅ Last Reaction Fields
        builder.Property(x => x.LastReactionTargetUserId)
            .HasConversion(v => v != null ? v.Value : (Guid?)null, v => v.HasValue ? new UserId(v.Value) : null)
            .Metadata.SetValueComparer(userIdNullableComparer);

        // ✅ Navigations
        builder.Metadata.FindNavigation(nameof(ChatRoom.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.PinnedMessages)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.PinnedMessages)
            .WithOne()
            .HasForeignKey(p => p.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}