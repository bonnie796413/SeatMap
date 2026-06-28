using BackEnd.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackEnd.Data.Configurations;

public class FloorMapConfiguration : IEntityTypeConfiguration<FloorMap>
{
    public void Configure(EntityTypeBuilder<FloorMap> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.FloorId).IsUnique();
        builder.Property(m => m.OriginalDxfPath).HasMaxLength(512);
        builder.Property(m => m.GeoJsonPath).HasMaxLength(512);
        builder.Property(m => m.ErrorMessage).HasColumnType("text");
        builder.Property(m => m.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(m => m.Status)
               .HasConversion<string>()
               .HasMaxLength(20);
    }
}
