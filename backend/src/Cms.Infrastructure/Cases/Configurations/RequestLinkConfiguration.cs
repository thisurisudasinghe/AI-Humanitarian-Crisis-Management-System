using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class RequestLinkConfiguration : IEntityTypeConfiguration<RequestLink>
{
    public void Configure(EntityTypeBuilder<RequestLink> builder)
    {
        builder.ToTable("request_links", "cases");
        builder.HasKey(l => l.Id).HasName("PK_request_links");

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(l => l.LinkedRequestId).HasColumnName("linked_request_id").IsRequired();
        builder.Property(l => l.LinkType).HasColumnName("link_type")
            .HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Request>().WithMany().HasForeignKey(l => l.RequestId)
            .HasConstraintName("FK_request_links_source").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Request>().WithMany().HasForeignKey(l => l.LinkedRequestId)
            .HasConstraintName("FK_request_links_target").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => l.RequestId).HasDatabaseName("IX_request_links_request_id");
        builder.HasIndex(l => l.LinkedRequestId).HasDatabaseName("IX_request_links_linked_request_id");
    }
}
