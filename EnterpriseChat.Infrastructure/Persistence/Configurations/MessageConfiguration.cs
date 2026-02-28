using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(x => x.Id);

        // ✅ حذف الـ Value Converters من هنا لأنها في DbContext
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.RoomId).IsRequired();
        builder.Property(x => x.SenderId).IsRequired();

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // ✅ Receipts navigation
        builder.Metadata
            .FindNavigation(nameof(Message.Receipts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Receipts)
            .WithOne()
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.RoomId);
        builder.HasIndex(x => x.CreatedAt);

        builder.Property(x => x.IsSystemMessage)
        .IsRequired()
        .HasDefaultValue(false);

        builder.Property(x => x.SystemMessageType)
            .HasConversion<string>();   // عشان يتحفظ string في الـ DB

        // Index عشان الـ queries تكون سريعة
        builder.HasIndex(x => new { x.RoomId, x.IsSystemMessage, x.CreatedAt });

        builder.Property(x => x.ReplyToMessageId)
    .IsRequired(false);

        // Self-referencing relationship
        builder.HasOne(x => x.ReplyToMessage)
            .WithMany()
            .HasForeignKey(x => x.ReplyToMessageId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}