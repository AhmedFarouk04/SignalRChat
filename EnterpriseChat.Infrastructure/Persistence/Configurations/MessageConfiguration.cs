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

        // MessageId
        builder.Property(x => x.Id)
            .HasConversion(
                id => id.Value,
                value => MessageId.From(value))
            .Metadata.SetValueComparer(
                new ValueComparer<MessageId>(
                    (a, b) => a.Value == b.Value,
                    v => v.Value.GetHashCode(),
                    v => MessageId.From(v.Value)));

        builder.Property(x => x.Id).IsRequired();

        // RoomId
        builder.Property(x => x.RoomId)
            .HasConversion(
                id => id.Value,
                value => new RoomId(value))
            .Metadata.SetValueComparer(
                new ValueComparer<RoomId>(
                    (a, b) => a.Value == b.Value,
                    v => v.Value.GetHashCode(),
                    v => new RoomId(v.Value)));

        builder.Property(x => x.RoomId).IsRequired();

        // SenderId
        builder.Property(x => x.SenderId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .Metadata.SetValueComparer(
                new ValueComparer<UserId>(
                    (a, b) => a.Value == b.Value,
                    v => v.Value.GetHashCode(),
                    v => new UserId(v.Value)));

        builder.Property(x => x.SenderId).IsRequired();

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Receipts navigation uses backing field
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
    }
}
