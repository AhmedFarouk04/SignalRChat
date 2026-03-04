using EnterpriseChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class MessageDeletionConfiguration : IEntityTypeConfiguration<MessageDeletion>
{
    public void Configure(EntityTypeBuilder<MessageDeletion> builder)
    {
        builder.ToTable("MessageDeletions");

        builder.HasKey(x => new { x.MessageId, x.UserId });

        builder.Property(x => x.MessageId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.DeletedAt).IsRequired();

        builder.HasIndex(x => x.UserId);
    }
}