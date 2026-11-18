using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class PartnerShareConfiguration : IEntityTypeConfiguration<PartnerShare>
{
    public void Configure(EntityTypeBuilder<PartnerShare> builder)
    {
        builder.ToTable("partner_shares", "cases");
        builder.HasKey(p => p.Id).HasName("PK_partner_shares");

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(p => p.OrganisationId).HasColumnName("organisation_id").IsRequired();
        builder.Property(p => p.SharedByUserId).HasColumnName("shared_by_user_id").IsRequired();
        builder.Property(p => p.SharedAt).HasColumnName("shared_at").IsRequired();
        builder.Property(p => p.RevokedAt).HasColumnName("revoked_at");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Request>().WithMany().HasForeignKey(p => p.RequestId)
            .HasConstraintName("FK_partner_shares_requests").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.RequestId).HasDatabaseName("IX_partner_shares_request_id");
    }
}
