using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class RequestStatusHistoryConfiguration : IEntityTypeConfiguration<RequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistory> builder)
    {
        builder.ToTable("request_status_history", "cases");
        builder.HasKey(h => h.Id).HasName("PK_request_status_history");

        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(h => h.FromStatus).HasColumnName("from_status")
            .HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(h => h.ToStatus).HasColumnName("to_status")
            .HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(h => h.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(h => h.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Request>().WithMany().HasForeignKey(h => h.RequestId)
            .HasConstraintName("FK_request_status_history_requests").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(h => new { h.RequestId, h.CreatedAt })
            .HasDatabaseName("IX_request_status_history_request_id_created_at");
    }
}
