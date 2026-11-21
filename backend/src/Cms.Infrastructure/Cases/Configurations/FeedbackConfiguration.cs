using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("feedback", "cases");
        builder.HasKey(f => f.Id).HasName("PK_feedback");

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(f => f.SubmittedByUserId).HasColumnName("submitted_by_user_id").IsRequired();
        builder.Property(f => f.Comment).HasColumnName("comment").IsRequired().HasMaxLength(2000);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Request>().WithMany().HasForeignKey(f => f.RequestId)
            .HasConstraintName("FK_feedback_requests").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => f.RequestId).HasDatabaseName("IX_feedback_request_id");
    }
}
