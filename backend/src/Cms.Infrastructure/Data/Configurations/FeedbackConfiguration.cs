using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("Feedback");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.RequestId)
            .IsRequired();

        builder.Property(f => f.UserId)
            .HasMaxLength(450);

        builder.Property(f => f.Rating)
            .IsRequired();

        builder.Property(f => f.Comments)
            .HasMaxLength(2000);

        builder.Property(f => f.SubmittedAt)
            .IsRequired();

        builder.HasOne(f => f.Request)
            .WithMany()
            .HasForeignKey(f => f.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.RequestId);
    }
}
