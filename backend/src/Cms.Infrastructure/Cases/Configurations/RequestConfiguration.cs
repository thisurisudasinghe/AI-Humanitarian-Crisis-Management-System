using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("requests", "cases");
        builder.HasKey(r => r.Id).HasName("PK_requests");

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.RequesterUserId).HasColumnName("requester_user_id").IsRequired();
        builder.Property(r => r.CrisisId).HasColumnName("crisis_id");
        builder.Property(r => r.AreaId).HasColumnName("area_id");
        builder.Property(r => r.Category).HasColumnName("category")
            .HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(r => r.Urgency).HasColumnName("urgency")
            .HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(r => r.Description).HasColumnName("description").IsRequired().HasMaxLength(2000);
        builder.Property(r => r.Lat).HasColumnName("lat").HasPrecision(18, 7);
        builder.Property(r => r.Lng).HasColumnName("lng").HasPrecision(18, 7);
        builder.Property(r => r.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(r => r.HouseholdSize).HasColumnName("household_size").IsRequired();
        builder.Property(r => r.AccessibilityNeeds).HasColumnName("accessibility_needs").HasMaxLength(2000);
        builder.Property(r => r.Status).HasColumnName("status")
            .HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(r => r.AssignedToUserId).HasColumnName("assigned_to_user_id");
        builder.Property(r => r.MergedIntoRequestId).HasColumnName("merged_into_request_id");
        builder.Property(r => r.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.Property(r => r.VerifiedAt).HasColumnName("verified_at");
        builder.Property(r => r.TriagedAt).HasColumnName("triaged_at");
        builder.Property(r => r.ClosedAt).HasColumnName("closed_at");
        builder.Property(r => r.RowVersion).HasColumnName("row_version").IsRowVersion();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Crisis>().WithMany().HasForeignKey(r => r.CrisisId)
            .HasConstraintName("FK_requests_crises").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Area>().WithMany().HasForeignKey(r => r.AreaId)
            .HasConstraintName("FK_requests_areas").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Request>().WithMany().HasForeignKey(r => r.MergedIntoRequestId)
            .HasConstraintName("FK_requests_merged_target").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("IX_requests_status_created_at");
        builder.HasIndex(r => r.RequesterUserId).HasDatabaseName("IX_requests_requester_user_id");
        builder.HasIndex(r => r.AreaId).HasDatabaseName("IX_requests_area_id");
        builder.HasIndex(r => r.AssignedToUserId).HasDatabaseName("IX_requests_assigned_to_user_id");
        builder.HasIndex(r => r.CrisisId).HasDatabaseName("IX_requests_crisis_id");
    }
}
