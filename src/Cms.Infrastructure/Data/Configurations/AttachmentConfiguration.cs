using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class AttachmentConfiguration
    : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.RequestId)
            .IsRequired();

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.FileUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.ContentType)
            .HasMaxLength(100);

        builder.Property(a => a.FileSize)
            .IsRequired();

        builder.Property(a => a.UploadedByUserId)
            .HasMaxLength(450);

        builder.Property(a => a.UploadedAt)
            .IsRequired();

        builder.HasOne(a => a.Request)
            .WithMany()
            .HasForeignKey(a => a.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.RequestId);
    }
}
