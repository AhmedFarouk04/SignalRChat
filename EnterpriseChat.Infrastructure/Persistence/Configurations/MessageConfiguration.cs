using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(
                id => id.Value,
                value => MessageId.From(value)
            )
            .IsRequired();

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasMany<MessageReceipt>()
         .WithOne()
         .HasForeignKey("MessageId")
         .OnDelete(DeleteBehavior.Cascade);


        builder.Property(x => x.RoomId)
            .HasConversion(
                id => id.Value,
                value => new RoomId(value))
            .IsRequired();

        builder.Property(x => x.SenderId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.Receipts);
    }
}
