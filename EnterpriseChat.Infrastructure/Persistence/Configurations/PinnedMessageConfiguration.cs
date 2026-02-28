// PinnedMessageConfiguration.cs
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class PinnedMessageConfiguration : IEntityTypeConfiguration<PinnedMessage>
{
    public void Configure(EntityTypeBuilder<PinnedMessage> builder)
    {
        builder.ToTable("PinnedMessages");

        // Primary Key - Guid عادي
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever(); // ✅ احنا بنولده manually

        // ✅ RoomId Value Object
        builder.Property(x => x.RoomId)
            .HasConversion(v => v.Value, v => new RoomId(v))
            .Metadata.SetValueComparer(new ValueComparer<RoomId>(
                (a, b) => a!.Value == b!.Value,
                v => v.Value.GetHashCode(),
                v => new RoomId(v.Value)));

        // ✅ MessageId Value Object
        builder.Property(x => x.MessageId)
            .HasConversion(v => v.Value, v => new MessageId(v))
            .Metadata.SetValueComparer(new ValueComparer<MessageId>(
                (a, b) => a!.Value == b!.Value,
                v => v.Value.GetHashCode(),
                v => new MessageId(v.Value)));

        // ✅ PinnedByUserId Value Object
        builder.Property(x => x.PinnedByUserId)
            .HasConversion(v => v.Value, v => new UserId(v))
            .Metadata.SetValueComparer(new ValueComparer<UserId>(
                (a, b) => a!.Value == b!.Value,
                v => v.Value.GetHashCode(),
                v => new UserId(v.Value)));

        builder.Property(x => x.PinnedAt).IsRequired();
        builder.Property(x => x.PinnedUntilUtc).IsRequired(false);

        // ✅ FK Relation
        builder.HasIndex(x => x.RoomId);
    }
}
