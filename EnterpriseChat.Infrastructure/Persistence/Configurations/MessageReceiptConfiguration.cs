using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EnterpriseChat.Infrastructure.Persistence.Configurations;
public sealed class MessageReceiptConfiguration
    : IEntityTypeConfiguration<MessageReceipt>
{
    public void Configure(EntityTypeBuilder<MessageReceipt> builder)
    {
        builder.ToTable("MessageReceipts");

        builder.HasKey(x => new
        {
            x.MessageId,
            x.UserId
        });

        builder.Property(x => x.MessageId)
            .HasConversion(
                id => id.Value,
                value => MessageId.From(value))
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.MessageId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.Status });
    }
}
