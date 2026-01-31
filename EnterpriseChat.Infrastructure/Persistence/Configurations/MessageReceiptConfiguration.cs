using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class MessageReceiptConfiguration : IEntityTypeConfiguration<MessageReceipt>
{
    public void Configure(EntityTypeBuilder<MessageReceipt> builder)
    {
        builder.ToTable("MessageReceipts");

        builder.HasKey(x => new { x.MessageId, x.UserId });

        // MessageId
        builder.Property(x => x.MessageId)
            .HasConversion(
                id => id.Value,
                value => MessageId.From(value))
            .Metadata.SetValueComparer(
                new ValueComparer<MessageId>(
                    (a, b) => a.Value == b.Value,
                    v => v.Value.GetHashCode(),
                    v => MessageId.From(v.Value)));

        builder.Property(x => x.MessageId).IsRequired();

        // UserId
        builder.Property(x => x.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .Metadata.SetValueComparer(
                new ValueComparer<UserId>(
                    (a, b) => a.Value == b.Value,
                    v => v.Value.GetHashCode(),
                    v => new UserId(v.Value)));

        builder.Property(x => x.UserId).IsRequired();

        // ✅ مهم جدًا: enum conversion
        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(x => x.MessageId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.Status });
    }
}
