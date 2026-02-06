// EnterpriseChat.Infrastructure/Persistence/Configurations/ReactionConfiguration.cs
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.ToTable("Reactions");

        builder.HasKey(x => x.Id);

        // Id
        builder.Property(x => x.Id)
            .HasConversion(
                id => id.Value,
                value => ReactionId.From(value));

        // MessageId
        builder.Property(x => x.MessageId)
            .HasConversion(
                id => id.Value,
                value => MessageId.From(value))
            .IsRequired();

        // UserId
        builder.Property(x => x.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        // Type
        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        // CreatedAt
        builder.Property(x => x.CreatedAt).IsRequired();

        // Indexes
        builder.HasIndex(x => x.MessageId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();

        // Foreign key
        builder.HasOne<Message>()
            .WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}