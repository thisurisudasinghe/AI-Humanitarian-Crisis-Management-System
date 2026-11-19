using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments", "cases");
        builder.HasKey(a => a.Id).HasName("PK_attachments");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(a => a.StoredFileName).HasColumnName("stored_file_name").IsRequired().HasMaxLength(255);
        builder.Property(a => a.OriginalFileName).HasColumnName("original_file_name").IsRequired().HasMaxLength(255);
        builder.Property(a => a.ContentType).HasColumnName("content_type").IsRequired().HasMaxLength(100);
        builder.Property(a => a.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(a => a.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Request>().WithMany().HasForeignKey(a => a.RequestId)
            .HasConstraintName("FK_attachments_requests").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.RequestId).HasDatabaseName("IX_attachments_request_id");
    }
}
